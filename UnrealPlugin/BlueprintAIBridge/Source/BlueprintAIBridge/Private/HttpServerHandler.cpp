#include "HttpServerHandler.h"
#include "HttpServerResponse.h"
#include "Engine/Blueprint.h"
#include "Engine/BlueprintGeneratedClass.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "Subsystems/AssetEditorSubsystem.h"
#include "Editor.h"
#include "Serialization/JsonSerializer.h"
#include "Serialization/JsonWriter.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "AssetToolsModule.h"
#include "Factories/BlueprintFactory.h"
#include "UObject/SavePackage.h"
#include "GameFramework/Actor.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/Character.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/GameModeBase.h"
#include "Components/ActorComponent.h"

bool FHttpServerHandler::HandleStatus(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
	TSharedPtr<FJsonObject> Response = MakeShared<FJsonObject>();
	Response->SetBoolField(TEXT("isConnected"), true);
	Response->SetStringField(TEXT("engineVersion"), FEngineVersion::Current().ToString());

	OnComplete(MakeJsonResponse(Response));
	return true;
}

bool FHttpServerHandler::HandleListBlueprints(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
	// FHttpServerModule dispatches handlers on the game thread in UE 5.x,
	// so we can access editor subsystems directly — no marshaling needed.
	TArray<TSharedPtr<FJsonValue>> BlueprintsList;

	UAssetEditorSubsystem* AssetEditorSubsystem = GEditor->GetEditorSubsystem<UAssetEditorSubsystem>();
	if (AssetEditorSubsystem)
	{
		TArray<UObject*> EditedAssets = AssetEditorSubsystem->GetAllEditedAssets();
		for (UObject* Asset : EditedAssets)
		{
			UBlueprint* Blueprint = Cast<UBlueprint>(Asset);
			if (Blueprint)
			{
				TSharedPtr<FJsonObject> BpInfo = MakeShared<FJsonObject>();
				BpInfo->SetStringField(TEXT("name"), Blueprint->GetName());
				BpInfo->SetStringField(TEXT("path"), Blueprint->GetPathName());
				BlueprintsList.Add(MakeShared<FJsonValueObject>(BpInfo));
			}
		}
	}

	// Return as plain array for our backend's expected format
	FString OutputString;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&OutputString);
	FJsonSerializer::Serialize(BlueprintsList, Writer);

	auto HttpResponse = FHttpServerResponse::Create(OutputString, TEXT("application/json"));
	OnComplete(MoveTemp(HttpResponse));
	return true;
}

bool FHttpServerHandler::HandleGetBlueprint(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
	if (!Request.QueryParams.Contains(TEXT("name")))
	{
		OnComplete(MakeErrorResponse(400, TEXT("Missing 'name' query parameter")));
		return true;
	}
	FString BlueprintName = Request.QueryParams[TEXT("name")];

	UBlueprint* Blueprint = FindBlueprintByName(BlueprintName);
	if (!Blueprint)
	{
		OnComplete(MakeErrorResponse(404, FString::Printf(TEXT("Blueprint '%s' not found in editor"), *BlueprintName)));
		return true;
	}

	// Get or create serializer for this blueprint
	TSharedPtr<FBlueprintSerializer>& Serializer = Serializers.FindOrAdd(BlueprintName);
	if (!Serializer.IsValid())
	{
		Serializer = MakeShared<FBlueprintSerializer>();
	}

	TSharedPtr<FJsonObject> Result = Serializer->SerializeBlueprint(Blueprint);
	OnComplete(MakeJsonResponse(Result));
	return true;
}

bool FHttpServerHandler::HandleApplyBlueprint(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
	if (!Request.QueryParams.Contains(TEXT("name")))
	{
		OnComplete(MakeErrorResponse(400, TEXT("Missing 'name' query parameter")));
		return true;
	}
	FString BlueprintName = Request.QueryParams[TEXT("name")];

	// Parse request body
	FString BodyString = FString(UTF8_TO_TCHAR(
		reinterpret_cast<const char*>(Request.Body.GetData())));

	TSharedPtr<FJsonObject> BodyJson;
	TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(BodyString);
	if (!FJsonSerializer::Deserialize(Reader, BodyJson) || !BodyJson.IsValid())
	{
		OnComplete(MakeErrorResponse(400, TEXT("Invalid JSON body")));
		return true;
	}

	UBlueprint* Blueprint = FindBlueprintByName(BlueprintName);
	if (!Blueprint)
	{
		TSharedPtr<FJsonObject> Response = MakeShared<FJsonObject>();
		Response->SetBoolField(TEXT("success"), false);
		Response->SetStringField(TEXT("error"),
			FString::Printf(TEXT("Blueprint '%s' not found in editor"), *BlueprintName));
		OnComplete(MakeJsonResponse(Response));
		return true;
	}

	bool bSuccess = Deserializer.ApplyFullSync(Blueprint, BodyJson);

	TSharedPtr<FJsonObject> Response = MakeShared<FJsonObject>();
	Response->SetBoolField(TEXT("success"), bSuccess);
	if (!bSuccess)
	{
		Response->SetStringField(TEXT("error"), TEXT("Failed to apply blueprint changes"));
	}
	OnComplete(MakeJsonResponse(Response));
	return true;
}

bool FHttpServerHandler::HandleCreateBlueprint(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
	// Parse request body: { "name": "BP_MyBlueprint", "path": "/Game/Blueprints", "parentClass": "Actor", "state": { ... } }
	FString BodyString = FString(UTF8_TO_TCHAR(
		reinterpret_cast<const char*>(Request.Body.GetData())));

	TSharedPtr<FJsonObject> BodyJson;
	TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(BodyString);
	if (!FJsonSerializer::Deserialize(Reader, BodyJson) || !BodyJson.IsValid())
	{
		OnComplete(MakeErrorResponse(400, TEXT("Invalid JSON body")));
		return true;
	}

	FString Name = BodyJson->GetStringField(TEXT("name"));
	FString Path = BodyJson->HasField(TEXT("path")) ? BodyJson->GetStringField(TEXT("path")) : TEXT("/Game/Blueprints");
	FString ParentClassName = BodyJson->HasField(TEXT("parentClass")) ? BodyJson->GetStringField(TEXT("parentClass")) : TEXT("Actor");

	if (Name.IsEmpty())
	{
		OnComplete(MakeErrorResponse(400, TEXT("Missing 'name' field")));
		return true;
	}

	// Resolve parent class
	UClass* ParentClass = AActor::StaticClass(); // default
	if (ParentClassName == TEXT("Pawn"))
	{
		ParentClass = APawn::StaticClass();
	}
	else if (ParentClassName == TEXT("Character"))
	{
		ParentClass = ACharacter::StaticClass();
	}
	else if (ParentClassName == TEXT("PlayerController"))
	{
		ParentClass = APlayerController::StaticClass();
	}
	else if (ParentClassName == TEXT("GameModeBase"))
	{
		ParentClass = AGameModeBase::StaticClass();
	}
	else if (ParentClassName == TEXT("ActorComponent"))
	{
		ParentClass = UActorComponent::StaticClass();
	}

	// Create the package and blueprint
	FString PackagePath = Path / Name;
	FString AssetName = Name;

	UPackage* Package = CreatePackage(*PackagePath);
	if (!Package)
	{
		OnComplete(MakeErrorResponse(500, FString::Printf(TEXT("Failed to create package at '%s'"), *PackagePath)));
		return true;
	}

	UBlueprint* NewBlueprint = FKismetEditorUtilities::CreateBlueprint(
		ParentClass,
		Package,
		FName(*AssetName),
		BPTYPE_Normal,
		UBlueprint::StaticClass(),
		UBlueprintGeneratedClass::StaticClass()
	);

	if (!NewBlueprint)
	{
		OnComplete(MakeErrorResponse(500, TEXT("Failed to create blueprint")));
		return true;
	}

	// Apply initial state if provided
	const TSharedPtr<FJsonObject>* StateJson;
	if (BodyJson->TryGetObjectField(TEXT("state"), StateJson))
	{
		Deserializer.ApplyFullSync(NewBlueprint, *StateJson);
	}

	// Mark dirty and save
	NewBlueprint->MarkPackageDirty();
	FAssetRegistryModule::AssetCreated(NewBlueprint);

	// Save the asset
	FSavePackageArgs SaveArgs;
	SaveArgs.TopLevelFlags = RF_Public | RF_Standalone;
	FString PackageFileName = FPackageName::LongPackageNameToFilename(PackagePath, FPackageName::GetAssetPackageExtension());
	UPackage::SavePackage(Package, NewBlueprint, *PackageFileName, SaveArgs);

	// Open in editor
	GEditor->GetEditorSubsystem<UAssetEditorSubsystem>()->OpenEditorForAsset(NewBlueprint);

	TSharedPtr<FJsonObject> Response = MakeShared<FJsonObject>();
	Response->SetBoolField(TEXT("success"), true);
	Response->SetStringField(TEXT("name"), NewBlueprint->GetName());
	Response->SetStringField(TEXT("path"), NewBlueprint->GetPathName());

	UE_LOG(LogTemp, Log, TEXT("BlueprintAIBridge: Created new blueprint '%s' at '%s'"), *Name, *PackagePath);

	OnComplete(MakeJsonResponse(Response));
	return true;
}

UBlueprint* FHttpServerHandler::FindBlueprintByName(const FString& Name) const
{
	UAssetEditorSubsystem* AssetEditorSubsystem = GEditor->GetEditorSubsystem<UAssetEditorSubsystem>();
	if (!AssetEditorSubsystem)
	{
		return nullptr;
	}

	TArray<UObject*> EditedAssets = AssetEditorSubsystem->GetAllEditedAssets();
	for (UObject* Asset : EditedAssets)
	{
		UBlueprint* Blueprint = Cast<UBlueprint>(Asset);
		if (Blueprint && Blueprint->GetName() == Name)
		{
			return Blueprint;
		}
	}

	return nullptr;
}

TUniquePtr<FHttpServerResponse> FHttpServerHandler::MakeJsonResponse(const TSharedPtr<FJsonObject>& Json)
{
	FString OutputString;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&OutputString);
	FJsonSerializer::Serialize(Json.ToSharedRef(), Writer);

	return FHttpServerResponse::Create(OutputString, TEXT("application/json"));
}

TUniquePtr<FHttpServerResponse> FHttpServerHandler::MakeErrorResponse(int32 Code, const FString& Message)
{
	TSharedPtr<FJsonObject> ErrorJson = MakeShared<FJsonObject>();
	ErrorJson->SetStringField(TEXT("error"), Message);

	FString OutputString;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&OutputString);
	FJsonSerializer::Serialize(ErrorJson.ToSharedRef(), Writer);

	return FHttpServerResponse::Create(OutputString, TEXT("application/json"));
}
