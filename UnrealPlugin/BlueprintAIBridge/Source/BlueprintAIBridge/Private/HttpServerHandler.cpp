#include "HttpServerHandler.h"
#include "HttpServerResponse.h"
#include "Engine/Blueprint.h"
#include "Engine/BlueprintGeneratedClass.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "Subsystems/AssetEditorSubsystem.h"
#include "Editor.h"
#include "Serialization/JsonSerializer.h"
#include "Serialization/JsonWriter.h"

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
