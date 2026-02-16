#include "BlueprintDeserializer.h"
#include "Engine/Blueprint.h"
#include "EdGraph/EdGraph.h"
#include "EdGraph/EdGraphNode.h"
#include "EdGraph/EdGraphPin.h"
#include "EdGraphSchema_K2.h"
#include "K2Node_CallFunction.h"
#include "K2Node_Event.h"
#include "K2Node_CustomEvent.h"
#include "K2Node_IfThenElse.h"
#include "K2Node_VariableGet.h"
#include "K2Node_VariableSet.h"
#include "K2Node_MacroInstance.h"
#include "K2Node_ExecutionSequence.h"
#include "Kismet2/BlueprintEditorUtils.h"
#include "Kismet2/KismetEditorUtilities.h"

bool FBlueprintDeserializer::ApplyFullSync(UBlueprint* Blueprint, const TSharedPtr<FJsonObject>& JsonState)
{
	if (!Blueprint || !JsonState.IsValid())
	{
		return false;
	}

	// Get or create the ubergraph
	UEdGraph* EventGraph = nullptr;
	if (Blueprint->UbergraphPages.Num() > 0)
	{
		EventGraph = Blueprint->UbergraphPages[0];
	}
	if (!EventGraph)
	{
		UE_LOG(LogTemp, Error, TEXT("BlueprintAIBridge: No event graph found in blueprint %s"), *Blueprint->GetName());
		return false;
	}

	// Clear existing nodes (except the default event nodes we can't remove)
	TArray<UEdGraphNode*> NodesToRemove;
	for (UEdGraphNode* Node : EventGraph->Nodes)
	{
		if (Node && Node->CanUserDeleteNode())
		{
			NodesToRemove.Add(Node);
		}
	}
	for (UEdGraphNode* Node : NodesToRemove)
	{
		EventGraph->RemoveNode(Node);
	}

	// Create nodes from JSON
	TMap<FString, UEdGraphNode*> NodeMap;
	const TArray<TSharedPtr<FJsonValue>>* NodesArray;
	if (JsonState->TryGetArrayField(TEXT("nodes"), NodesArray))
	{
		for (const TSharedPtr<FJsonValue>& NodeVal : *NodesArray)
		{
			TSharedPtr<FJsonObject> NodeJson = NodeVal->AsObject();
			if (!NodeJson.IsValid()) continue;

			FString NodeId = NodeJson->GetStringField(TEXT("id"));
			UEdGraphNode* NewNode = CreateNodeFromJson(EventGraph, NodeJson);
			if (NewNode)
			{
				NodeMap.Add(NodeId, NewNode);
			}
		}
	}

	// Wire connections
	const TArray<TSharedPtr<FJsonValue>>* ConnectionsArray;
	if (JsonState->TryGetArrayField(TEXT("connections"), ConnectionsArray))
	{
		WireConnections(EventGraph, *ConnectionsArray, NodeMap);
	}

	// Compile the blueprint
	FBlueprintEditorUtils::MarkBlueprintAsModified(Blueprint);
	FKismetEditorUtilities::CompileBlueprint(Blueprint);

	UE_LOG(LogTemp, Log, TEXT("BlueprintAIBridge: Applied full sync to %s (%d nodes)"),
		*Blueprint->GetName(), NodeMap.Num());

	return true;
}

UEdGraphNode* FBlueprintDeserializer::CreateNodeFromJson(UEdGraph* Graph, const TSharedPtr<FJsonObject>& NodeJson)
{
	FString Title = NodeJson->GetStringField(TEXT("title"));
	FString Style = NodeJson->GetStringField(TEXT("style"));
	double PosX = NodeJson->GetNumberField(TEXT("positionX"));
	double PosY = NodeJson->GetNumberField(TEXT("positionY"));

	// For now, create CallFunction nodes as the most common type.
	// A more complete implementation would match by title/category to find
	// the appropriate UFunction or node class.
	UK2Node_CallFunction* FuncNode = NewObject<UK2Node_CallFunction>(Graph);
	FuncNode->CreateNewGuid();
	FuncNode->PostPlacedNewNode();
	FuncNode->NodePosX = static_cast<int32>(PosX);
	FuncNode->NodePosY = static_cast<int32>(PosY);
	FuncNode->NodeComment = Title;

	Graph->AddNode(FuncNode, false, false);
	FuncNode->AllocateDefaultPins();

	return FuncNode;
}

bool FBlueprintDeserializer::WireConnections(UEdGraph* Graph,
	const TArray<TSharedPtr<FJsonValue>>& Connections,
	const TMap<FString, UEdGraphNode*>& NodeMap)
{
	int32 WiredCount = 0;

	for (const TSharedPtr<FJsonValue>& ConnVal : Connections)
	{
		TSharedPtr<FJsonObject> ConnJson = ConnVal->AsObject();
		if (!ConnJson.IsValid()) continue;

		FString SourceNodeId = ConnJson->GetStringField(TEXT("sourceNodeId"));
		FString TargetNodeId = ConnJson->GetStringField(TEXT("targetNodeId"));

		UEdGraphNode* const* SourceNodePtr = NodeMap.Find(SourceNodeId);
		UEdGraphNode* const* TargetNodePtr = NodeMap.Find(TargetNodeId);

		if (!SourceNodePtr || !TargetNodePtr) continue;

		// Try to match pins by name from the JSON
		// This is a simplified approach - a full implementation would use
		// the pin ID mapping registry
		FString SourcePinName = ConnJson->GetStringField(TEXT("sourcePinId"));
		FString TargetPinName = ConnJson->GetStringField(TEXT("targetPinId"));

		// For now, try to connect exec pins (then → execute) as the most common case
		UEdGraphPin* SourcePin = FindPinByName(*SourceNodePtr, TEXT("then"), EGPD_Output);
		UEdGraphPin* TargetPin = FindPinByName(*TargetNodePtr, TEXT("execute"), EGPD_Input);

		if (SourcePin && TargetPin)
		{
			SourcePin->MakeLinkTo(TargetPin);
			WiredCount++;
		}
	}

	UE_LOG(LogTemp, Log, TEXT("BlueprintAIBridge: Wired %d connections"), WiredCount);
	return WiredCount > 0;
}

UEdGraphPin* FBlueprintDeserializer::FindPinByName(UEdGraphNode* Node, const FString& PinName, EEdGraphPinDirection Direction)
{
	if (!Node) return nullptr;

	for (UEdGraphPin* Pin : Node->Pins)
	{
		if (Pin->Direction == Direction && Pin->GetDisplayName().ToString().Equals(PinName, ESearchCase::IgnoreCase))
		{
			return Pin;
		}
	}

	// Fallback: match by PinName field
	for (UEdGraphPin* Pin : Node->Pins)
	{
		if (Pin->Direction == Direction && Pin->PinName.ToString().Equals(PinName, ESearchCase::IgnoreCase))
		{
			return Pin;
		}
	}

	return nullptr;
}
