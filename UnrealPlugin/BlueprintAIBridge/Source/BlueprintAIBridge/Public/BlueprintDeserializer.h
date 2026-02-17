#pragma once

#include "CoreMinimal.h"
#include "Dom/JsonObject.h"

class UBlueprint;
class UEdGraph;

/**
 * Applies a full-sync JSON blueprint state to a UE Blueprint graph.
 * Clears the existing graph and rebuilds nodes + connections from the JSON payload.
 */
class BLUEPRINTAIBRIDGE_API FBlueprintDeserializer
{
public:
	bool ApplyFullSync(UBlueprint* Blueprint, const TSharedPtr<FJsonObject>& JsonState);

private:
	UEdGraphNode* CreateNodeFromJson(UBlueprint* Blueprint, UEdGraph* Graph, const TSharedPtr<FJsonObject>& NodeJson);
	UEdGraphNode* CreateEventNode(UBlueprint* Blueprint, UEdGraph* Graph, const FString& Title, int32 PosX, int32 PosY);
	UEdGraphNode* CreateFunctionNode(UEdGraph* Graph, const FString& Title, int32 PosX, int32 PosY);
	UEdGraphNode* CreateFlowControlNode(UEdGraph* Graph, const FString& Title, int32 PosX, int32 PosY);
	UEdGraphNode* CreatePureNode(UEdGraph* Graph, const FString& Title, int32 PosX, int32 PosY);

	bool WireConnections(UEdGraph* Graph, const TArray<TSharedPtr<FJsonValue>>& Connections,
		const TMap<FString, UEdGraphNode*>& NodeMap,
		const TMap<FString, TMap<FString, FString>>& PinIdToNameMap);

	void CreateVariablesFromJson(UBlueprint* Blueprint, const TArray<TSharedPtr<FJsonValue>>& VariablesArray);
	UEdGraphNode* CreateVariableNode(UBlueprint* Blueprint, UEdGraph* Graph, const FString& Title, const TSharedPtr<FJsonObject>& NodeJson, int32 PosX, int32 PosY);
	FEdGraphPinType MapPinTypeFromString(const FString& TypeStr);

	UEdGraphPin* FindPinByName(UEdGraphNode* Node, const FString& PinName, EEdGraphPinDirection Direction);
	UFunction* FindFunctionByDisplayName(const FString& DisplayName);

	/** Maps JSON pin ID → pin display name, per node ID */
	TMap<FString, TMap<FString, FString>> PinNameMap;

	/** Cache for FindFunctionByDisplayName to avoid repeated TObjectIterator scans */
	TMap<FString, UFunction*> FunctionCache;
};
