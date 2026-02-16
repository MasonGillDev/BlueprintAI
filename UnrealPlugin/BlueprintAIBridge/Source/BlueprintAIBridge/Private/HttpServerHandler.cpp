#include "HttpServerHandler.h"
#include "HttpServerResponse.h"
#include "Engine/Blueprint.h"
#include "Engine/BlueprintGeneratedClass.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "Subsystems/AssetEditorSubsystem.h"
#include "Editor.h"
#include "Serialization/JsonSerializer.h"
#include "Serialization/JsonWriter.h"
#include "Async/Async.h"
#include "Misc/EngineVersionComparison.h"

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
	// Must access editor state on game thread
	TSharedPtr<TPromise<TArray<TSharedPtr<FJsonValue>>>> Promise = MakeShared<TPromise<TArray<TSharedPtr<FJsonValue>>>>();
	TFuture<TArray<TSharedPtr<FJsonValue>>> Future = Promise->GetFuture();

	AsyncTask(ENamedThreads::GameThread, [Promise]()
	{
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

		Promise->SetValue(BlueprintsList);
	});

	// Wait for game thread result (with timeout)
	Future.WaitFor(FTimespan::FromSeconds(5.0));
	if (Future.IsReady())
	{
		TArray<TSharedPtr<FJsonValue>> Blueprints = Future.Get();
		TSharedPtr<FJsonObject> Response = MakeShared<FJsonObject>();
		Response->SetArrayField(TEXT("blueprints"), Blueprints);

		// Return as plain array for our backend's expected format
		FString OutputString;
		TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&OutputString);
		FJsonSerializer::Serialize(Blueprints, Writer);

		auto HttpResponse = FHttpServerResponse::Create(OutputString, TEXT("application/json"));
		OnComplete(MoveTemp(HttpResponse));
	}
	else
	{
		OnComplete(MakeErrorResponse(500, TEXT("Timed out accessing editor state")));
	}

	return true;
}

bool FHttpServerHandler::HandleGetBlueprint(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
	FString BlueprintName;
	if (!Request.QueryParams.Contains(TEXT("name")))
	{
		OnComplete(MakeErrorResponse(400, TEXT("Missing 'name' query parameter")));
		return true;
	}
	BlueprintName = Request.QueryParams[TEXT("name")];

	TSharedPtr<TPromise<TSharedPtr<FJsonObject>>> Promise = MakeShared<TPromise<TSharedPtr<FJsonObject>>>();
	TFuture<TSharedPtr<FJsonObject>> Future = Promise->GetFuture();

	// Capture serializers map reference
	TMap<FString, TSharedPtr<FBlueprintSerializer>>* SerializersPtr = &Serializers;

	AsyncTask(ENamedThreads::GameThread, [Promise, BlueprintName, SerializersPtr]()
	{
		UAssetEditorSubsystem* AssetEditorSubsystem = GEditor->GetEditorSubsystem<UAssetEditorSubsystem>();
		UBlueprint* Blueprint = nullptr;

		if (AssetEditorSubsystem)
		{
			TArray<UObject*> EditedAssets = AssetEditorSubsystem->GetAllEditedAssets();
			for (UObject* Asset : EditedAssets)
			{
				UBlueprint* Bp = Cast<UBlueprint>(Asset);
				if (Bp && Bp->GetName() == BlueprintName)
				{
					Blueprint = Bp;
					break;
				}
			}
		}

		if (!Blueprint)
		{
			Promise->SetValue(nullptr);
			return;
		}

		// Get or create serializer for this blueprint
		TSharedPtr<FBlueprintSerializer>& Serializer = SerializersPtr->FindOrAdd(BlueprintName);
		if (!Serializer.IsValid())
		{
			Serializer = MakeShared<FBlueprintSerializer>();
		}

		TSharedPtr<FJsonObject> Result = Serializer->SerializeBlueprint(Blueprint);
		Promise->SetValue(Result);
	});

	Future.WaitFor(FTimespan::FromSeconds(10.0));
	if (Future.IsReady())
	{
		TSharedPtr<FJsonObject> Result = Future.Get();
		if (Result.IsValid())
		{
			OnComplete(MakeJsonResponse(Result));
		}
		else
		{
			OnComplete(MakeErrorResponse(404, FString::Printf(TEXT("Blueprint '%s' not found in editor"), *BlueprintName)));
		}
	}
	else
	{
		OnComplete(MakeErrorResponse(500, TEXT("Timed out serializing blueprint")));
	}

	return true;
}

bool FHttpServerHandler::HandleApplyBlueprint(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
	FString BlueprintName;
	if (!Request.QueryParams.Contains(TEXT("name")))
	{
		OnComplete(MakeErrorResponse(400, TEXT("Missing 'name' query parameter")));
		return true;
	}
	BlueprintName = Request.QueryParams[TEXT("name")];

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

	TSharedPtr<TPromise<bool>> Promise = MakeShared<TPromise<bool>>();
	TFuture<bool> Future = Promise->GetFuture();

	FBlueprintDeserializer* DeserializerPtr = &Deserializer;

	AsyncTask(ENamedThreads::GameThread, [Promise, BlueprintName, BodyJson, DeserializerPtr]()
	{
		UAssetEditorSubsystem* AssetEditorSubsystem = GEditor->GetEditorSubsystem<UAssetEditorSubsystem>();
		UBlueprint* Blueprint = nullptr;

		if (AssetEditorSubsystem)
		{
			TArray<UObject*> EditedAssets = AssetEditorSubsystem->GetAllEditedAssets();
			for (UObject* Asset : EditedAssets)
			{
				UBlueprint* Bp = Cast<UBlueprint>(Asset);
				if (Bp && Bp->GetName() == BlueprintName)
				{
					Blueprint = Bp;
					break;
				}
			}
		}

		if (!Blueprint)
		{
			Promise->SetValue(false);
			return;
		}

		bool bSuccess = DeserializerPtr->ApplyFullSync(Blueprint, BodyJson);
		Promise->SetValue(bSuccess);
	});

	Future.WaitFor(FTimespan::FromSeconds(30.0));
	if (Future.IsReady())
	{
		bool bSuccess = Future.Get();
		TSharedPtr<FJsonObject> Response = MakeShared<FJsonObject>();
		Response->SetBoolField(TEXT("success"), bSuccess);
		if (!bSuccess)
		{
			Response->SetStringField(TEXT("error"),
				FString::Printf(TEXT("Blueprint '%s' not found or failed to apply"), *BlueprintName));
		}
		OnComplete(MakeJsonResponse(Response));
	}
	else
	{
		OnComplete(MakeErrorResponse(500, TEXT("Timed out applying blueprint changes")));
	}

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
