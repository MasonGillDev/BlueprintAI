#include "BlueprintAIBridgeModule.h"
#include "HttpServerHandler.h"
#include "HttpServerModule.h"
#include "IHttpRouter.h"
#include "Misc/ConfigCacheIni.h"

#define LOCTEXT_NAMESPACE "FBlueprintAIBridgeModule"

static TSharedPtr<FHttpServerHandler> GHandler;

void FBlueprintAIBridgeModule::StartupModule()
{
	// Bind to all interfaces so other devices on the network can connect
	GConfig->SetString(TEXT("HTTPServer.Listeners"), TEXT("DefaultBindAddress"), TEXT("0.0.0.0"), GEngineIni);

	FHttpServerModule& HttpServerModule = FHttpServerModule::Get();
	HttpRouter = HttpServerModule.GetHttpRouter(ListenPort);

	if (!HttpRouter.IsValid())
	{
		UE_LOG(LogTemp, Error, TEXT("BlueprintAIBridge: Failed to get HTTP router on port %d"), ListenPort);
		return;
	}

	GHandler = MakeShared<FHttpServerHandler>();
	RegisterRoutes();
	HttpServerModule.StartAllListeners();

	UE_LOG(LogTemp, Log, TEXT("BlueprintAIBridge: HTTP server started on port %d"), ListenPort);
}

void FBlueprintAIBridgeModule::ShutdownModule()
{
	UnregisterRoutes();
	GHandler.Reset();

	UE_LOG(LogTemp, Log, TEXT("BlueprintAIBridge: HTTP server shut down"));
}

void FBlueprintAIBridgeModule::RegisterRoutes()
{
	if (!HttpRouter.IsValid() || !GHandler.IsValid())
	{
		return;
	}

	// GET /api/status
	RouteHandles.Add(HttpRouter->BindRoute(
		FHttpPath(TEXT("/api/status")),
		EHttpServerRequestVerbs::VERB_GET,
		FHttpRequestHandler::CreateLambda([](const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
		{
			return GHandler->HandleStatus(Request, OnComplete);
		})
	));

	// GET /api/blueprints
	RouteHandles.Add(HttpRouter->BindRoute(
		FHttpPath(TEXT("/api/blueprints")),
		EHttpServerRequestVerbs::VERB_GET,
		FHttpRequestHandler::CreateLambda([](const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
		{
			return GHandler->HandleListBlueprints(Request, OnComplete);
		})
	));

	// GET /api/blueprint
	RouteHandles.Add(HttpRouter->BindRoute(
		FHttpPath(TEXT("/api/blueprint")),
		EHttpServerRequestVerbs::VERB_GET,
		FHttpRequestHandler::CreateLambda([](const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
		{
			return GHandler->HandleGetBlueprint(Request, OnComplete);
		})
	));

	// POST /api/blueprint/apply
	RouteHandles.Add(HttpRouter->BindRoute(
		FHttpPath(TEXT("/api/blueprint/apply")),
		EHttpServerRequestVerbs::VERB_POST,
		FHttpRequestHandler::CreateLambda([](const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
		{
			return GHandler->HandleApplyBlueprint(Request, OnComplete);
		})
	));

	// POST /api/blueprint/create
	RouteHandles.Add(HttpRouter->BindRoute(
		FHttpPath(TEXT("/api/blueprint/create")),
		EHttpServerRequestVerbs::VERB_POST,
		FHttpRequestHandler::CreateLambda([](const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
		{
			return GHandler->HandleCreateBlueprint(Request, OnComplete);
		})
	));
}

void FBlueprintAIBridgeModule::UnregisterRoutes()
{
	if (HttpRouter.IsValid())
	{
		for (const FHttpRouteHandle& Handle : RouteHandles)
		{
			HttpRouter->UnbindRoute(Handle);
		}
	}
	RouteHandles.Empty();
}

#undef LOCTEXT_NAMESPACE

IMPLEMENT_MODULE(FBlueprintAIBridgeModule, BlueprintAIBridge)
