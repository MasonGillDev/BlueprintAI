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
	/**
	 * Apply a full blueprint state (from BlueprintAI JSON) to an existing UE Blueprint.
	 * Clears the event graph and recreates all nodes and connections.
	 * Must be called on the game thread.
	 *
	 * @return true if successful
	 */
	bool ApplyFullSync(UBlueprint* Blueprint, const TSharedPtr<FJsonObject>& JsonState);

private:
	UEdGraphNode* CreateNodeFromJson(UEdGraph* Graph, const TSharedPtr<FJsonObject>& NodeJson);
	bool WireConnections(UEdGraph* Graph, const TArray<TSharedPtr<FJsonValue>>& Connections,
		const TMap<FString, UEdGraphNode*>& NodeMap);
	UEdGraphPin* FindPinByName(UEdGraphNode* Node, const FString& PinName, EEdGraphPinDirection Direction);
};
