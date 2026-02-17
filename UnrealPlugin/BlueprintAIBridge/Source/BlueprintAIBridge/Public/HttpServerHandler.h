#pragma once

#include "CoreMinimal.h"
#include "HttpResultCallback.h"
#include "HttpServerRequest.h"
#include "BlueprintSerializer.h"
#include "BlueprintDeserializer.h"

/**
 * Handles all HTTP requests for the BlueprintAI bridge plugin.
 * Routes:
 *   GET  /api/status                - Health check + engine version
 *   GET  /api/blueprints            - List open blueprints in editor
 *   GET  /api/blueprint?name=X      - Export blueprint graph as JSON
 *   POST /api/blueprint/apply?name=X - Apply delta/full-sync to blueprint
 *   POST /api/blueprint/create       - Create a new blueprint asset
 */
class BLUEPRINTAIBRIDGE_API FHttpServerHandler
{
public:
	bool HandleStatus(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete);
	bool HandleListBlueprints(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete);
	bool HandleGetBlueprint(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete);
	bool HandleApplyBlueprint(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete);
	bool HandleCreateBlueprint(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete);

private:
	UBlueprint* FindBlueprintByName(const FString& Name) const;
	TUniquePtr<FHttpServerResponse> MakeJsonResponse(const TSharedPtr<FJsonObject>& Json);
	TUniquePtr<FHttpServerResponse> MakeErrorResponse(int32 Code, const FString& Message);

	/** Per-blueprint serializer instances maintain ID mapping registries */
	TMap<FString, TSharedPtr<FBlueprintSerializer>> Serializers;

	FBlueprintDeserializer Deserializer;
};
