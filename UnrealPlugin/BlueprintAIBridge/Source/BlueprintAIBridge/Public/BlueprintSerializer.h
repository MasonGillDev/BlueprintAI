#pragma once

#include "CoreMinimal.h"
#include "Dom/JsonObject.h"
#include "Dom/JsonValue.h"

class UBlueprint;
class UEdGraph;
class UK2Node;
class UEdGraphPin;

/**
 * Serializes UE Blueprint graphs into JSON compatible with the BlueprintAI domain model.
 * Maintains a mapping registry so IDs can be resolved back during deserialization.
 */
class BLUEPRINTAIBRIDGE_API FBlueprintSerializer
{
public:
	/**
	 * Serialize an entire blueprint into a JSON object.
	 * Populates internal mapping registry for round-trip support.
	 */
	TSharedPtr<FJsonObject> SerializeBlueprint(UBlueprint* Blueprint);

	/** Get the node mapping (generated GUID -> UEdGraphNode*) */
	const TMap<FString, class UEdGraphNode*>& GetNodeMap() const { return NodeMap; }

	/** Get the pin mapping (generated GUID -> UEdGraphPin*) */
	const TMap<FString, UEdGraphPin*>& GetPinMap() const { return PinMap; }

	/** Clear all mappings */
	void ClearMappings();

private:
	TSharedPtr<FJsonObject> SerializeNode(UK2Node* Node);
	TSharedPtr<FJsonObject> SerializePin(UEdGraphPin* Pin);
	TArray<TSharedPtr<FJsonValue>> SerializeConnections(UEdGraph* Graph);
	FString MapNodeStyle(UK2Node* Node) const;
	FString MapPinType(UEdGraphPin* Pin) const;

	TMap<FString, UEdGraphNode*> NodeMap;
	TMap<FString, UEdGraphPin*> PinMap;
};
