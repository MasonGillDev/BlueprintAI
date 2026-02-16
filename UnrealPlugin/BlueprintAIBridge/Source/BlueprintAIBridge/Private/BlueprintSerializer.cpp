#include "BlueprintSerializer.h"
#include "Engine/Blueprint.h"
#include "EdGraph/EdGraph.h"
#include "EdGraph/EdGraphNode.h"
#include "EdGraph/EdGraphPin.h"
#include "EdGraphSchema_K2.h"
#include "K2Node.h"
#include "K2Node_Event.h"
#include "K2Node_CustomEvent.h"
#include "K2Node_CallFunction.h"
#include "K2Node_IfThenElse.h"
#include "K2Node_ExecutionSequence.h"
#include "K2Node_VariableGet.h"
#include "K2Node_VariableSet.h"
#include "K2Node_MacroInstance.h"
#include "K2Node_Composite.h"
#include "K2Node_Knot.h"
#include "Serialization/JsonSerializer.h"

TSharedPtr<FJsonObject> FBlueprintSerializer::SerializeBlueprint(UBlueprint* Blueprint)
{
	ClearMappings();

	TSharedPtr<FJsonObject> Root = MakeShared<FJsonObject>();
	Root->SetStringField(TEXT("name"), Blueprint->GetName());

	// Gather all event graphs
	TArray<UEdGraph*> Graphs;
	for (UEdGraph* Graph : Blueprint->UbergraphPages)
	{
		Graphs.Add(Graph);
	}
	for (UEdGraph* Graph : Blueprint->FunctionGraphs)
	{
		Graphs.Add(Graph);
	}

	// Serialize nodes
	TArray<TSharedPtr<FJsonValue>> NodesArray;
	for (UEdGraph* Graph : Graphs)
	{
		for (UEdGraphNode* Node : Graph->Nodes)
		{
			UK2Node* K2Node = Cast<UK2Node>(Node);
			if (K2Node)
			{
				TSharedPtr<FJsonObject> NodeJson = SerializeNode(K2Node);
				if (NodeJson.IsValid())
				{
					NodesArray.Add(MakeShared<FJsonValueObject>(NodeJson));
				}
			}
		}
	}
	Root->SetArrayField(TEXT("nodes"), NodesArray);

	// Serialize connections
	TArray<TSharedPtr<FJsonValue>> AllConnections;
	for (UEdGraph* Graph : Graphs)
	{
		TArray<TSharedPtr<FJsonValue>> GraphConnections = SerializeConnections(Graph);
		AllConnections.Append(GraphConnections);
	}
	Root->SetArrayField(TEXT("connections"), AllConnections);

	// Comments and variables as empty arrays for now
	Root->SetArrayField(TEXT("comments"), TArray<TSharedPtr<FJsonValue>>());
	Root->SetArrayField(TEXT("variables"), TArray<TSharedPtr<FJsonValue>>());

	return Root;
}

TSharedPtr<FJsonObject> FBlueprintSerializer::SerializeNode(UK2Node* Node)
{
	TSharedPtr<FJsonObject> NodeJson = MakeShared<FJsonObject>();

	// Generate stable ID and store mapping
	FString NodeId = FGuid::NewGuid().ToString(EGuidFormats::DigitsWithHyphens);
	NodeMap.Add(NodeId, Node);

	NodeJson->SetStringField(TEXT("id"), NodeId);
	NodeJson->SetStringField(TEXT("title"), Node->GetNodeTitle(ENodeTitleType::FullTitle).ToString());
	NodeJson->SetStringField(TEXT("category"), Node->GetNodeCategory().ToString());
	NodeJson->SetStringField(TEXT("style"), MapNodeStyle(Node));
	NodeJson->SetNumberField(TEXT("positionX"), Node->NodePosX);
	NodeJson->SetNumberField(TEXT("positionY"), Node->NodePosY);
	NodeJson->SetBoolField(TEXT("isCompact"), Node->ShouldDrawCompact());

	// Serialize pins
	TArray<TSharedPtr<FJsonValue>> InputPins;
	TArray<TSharedPtr<FJsonValue>> OutputPins;

	for (UEdGraphPin* Pin : Node->Pins)
	{
		if (Pin->bHidden)
		{
			continue;
		}

		TSharedPtr<FJsonObject> PinJson = SerializePin(Pin);
		if (PinJson.IsValid())
		{
			if (Pin->Direction == EGPD_Input)
			{
				InputPins.Add(MakeShared<FJsonValueObject>(PinJson));
			}
			else
			{
				OutputPins.Add(MakeShared<FJsonValueObject>(PinJson));
			}
		}
	}

	NodeJson->SetArrayField(TEXT("inputPins"), InputPins);
	NodeJson->SetArrayField(TEXT("outputPins"), OutputPins);

	return NodeJson;
}

TSharedPtr<FJsonObject> FBlueprintSerializer::SerializePin(UEdGraphPin* Pin)
{
	TSharedPtr<FJsonObject> PinJson = MakeShared<FJsonObject>();

	FString PinId = FGuid::NewGuid().ToString(EGuidFormats::DigitsWithHyphens);
	PinMap.Add(PinId, Pin);

	PinJson->SetStringField(TEXT("id"), PinId);
	PinJson->SetStringField(TEXT("name"), Pin->GetDisplayName().ToString());
	PinJson->SetStringField(TEXT("type"), MapPinType(Pin));
	PinJson->SetStringField(TEXT("direction"), Pin->Direction == EGPD_Input ? TEXT("Input") : TEXT("Output"));
	PinJson->SetBoolField(TEXT("isConnected"), Pin->LinkedTo.Num() > 0);

	if (!Pin->DefaultValue.IsEmpty())
	{
		PinJson->SetStringField(TEXT("defaultValue"), Pin->DefaultValue);
	}

	// SubType for struct/object pins
	if (Pin->PinType.PinSubCategoryObject.IsValid())
	{
		PinJson->SetStringField(TEXT("subType"), Pin->PinType.PinSubCategoryObject->GetName());
	}

	return PinJson;
}

TArray<TSharedPtr<FJsonValue>> FBlueprintSerializer::SerializeConnections(UEdGraph* Graph)
{
	TArray<TSharedPtr<FJsonValue>> Connections;
	TSet<FString> ProcessedConnections;

	for (UEdGraphNode* Node : Graph->Nodes)
	{
		for (UEdGraphPin* Pin : Node->Pins)
		{
			if (Pin->Direction != EGPD_Output)
			{
				continue;
			}

			for (UEdGraphPin* LinkedPin : Pin->LinkedTo)
			{
				// Find IDs from our maps
				FString SourcePinId;
				FString TargetPinId;
				FString SourceNodeId;
				FString TargetNodeId;

				for (const auto& Pair : PinMap)
				{
					if (Pair.Value == Pin) SourcePinId = Pair.Key;
					if (Pair.Value == LinkedPin) TargetPinId = Pair.Key;
				}

				for (const auto& Pair : NodeMap)
				{
					if (Pair.Value == Node) SourceNodeId = Pair.Key;
					if (Pair.Value == LinkedPin->GetOwningNode()) TargetNodeId = Pair.Key;
				}

				if (SourcePinId.IsEmpty() || TargetPinId.IsEmpty())
				{
					continue;
				}

				// Deduplicate
				FString ConnectionKey = SourcePinId + TEXT("->") + TargetPinId;
				if (ProcessedConnections.Contains(ConnectionKey))
				{
					continue;
				}
				ProcessedConnections.Add(ConnectionKey);

				TSharedPtr<FJsonObject> ConnJson = MakeShared<FJsonObject>();
				ConnJson->SetStringField(TEXT("id"), FGuid::NewGuid().ToString(EGuidFormats::DigitsWithHyphens));
				ConnJson->SetStringField(TEXT("sourceNodeId"), SourceNodeId);
				ConnJson->SetStringField(TEXT("sourcePinId"), SourcePinId);
				ConnJson->SetStringField(TEXT("targetNodeId"), TargetNodeId);
				ConnJson->SetStringField(TEXT("targetPinId"), TargetPinId);
				ConnJson->SetStringField(TEXT("pinType"), MapPinType(Pin));

				Connections.Add(MakeShared<FJsonValueObject>(ConnJson));
			}
		}
	}

	return Connections;
}

FString FBlueprintSerializer::MapNodeStyle(UK2Node* Node) const
{
	if (Cast<UK2Node_Event>(Node) || Cast<UK2Node_CustomEvent>(Node))
	{
		return TEXT("Event");
	}

	if (UK2Node_CallFunction* FuncNode = Cast<UK2Node_CallFunction>(Node))
	{
		if (FuncNode->IsNodePure())
		{
			return TEXT("Pure");
		}
		return TEXT("Function");
	}

	if (Cast<UK2Node_IfThenElse>(Node) || Cast<UK2Node_ExecutionSequence>(Node))
	{
		return TEXT("FlowControl");
	}

	if (Cast<UK2Node_VariableGet>(Node) || Cast<UK2Node_VariableSet>(Node))
	{
		return TEXT("Variable");
	}

	if (Cast<UK2Node_MacroInstance>(Node))
	{
		return TEXT("Macro");
	}

	// Default to Function for unrecognized node types
	return TEXT("Function");
}

FString FBlueprintSerializer::MapPinType(UEdGraphPin* Pin) const
{
	const FName& Category = Pin->PinType.PinCategory;

	if (Category == UEdGraphSchema_K2::PC_Exec) return TEXT("Exec");
	if (Category == UEdGraphSchema_K2::PC_Boolean) return TEXT("Bool");
	if (Category == UEdGraphSchema_K2::PC_Int) return TEXT("Int");
	if (Category == UEdGraphSchema_K2::PC_Real || Category == UEdGraphSchema_K2::PC_Float) return TEXT("Float");
	if (Category == UEdGraphSchema_K2::PC_String) return TEXT("String");
	if (Category == UEdGraphSchema_K2::PC_Name) return TEXT("Name");
	if (Category == UEdGraphSchema_K2::PC_Text) return TEXT("Text");
	if (Category == UEdGraphSchema_K2::PC_Byte) return TEXT("Byte");
	if (Category == UEdGraphSchema_K2::PC_Object || Category == UEdGraphSchema_K2::PC_SoftObject) return TEXT("Object");
	if (Category == UEdGraphSchema_K2::PC_Class || Category == UEdGraphSchema_K2::PC_SoftClass) return TEXT("Class");
	if (Category == UEdGraphSchema_K2::PC_Delegate || Category == UEdGraphSchema_K2::PC_MCDelegate) return TEXT("Delegate");
	if (Category == UEdGraphSchema_K2::PC_Wildcard) return TEXT("Wildcard");
	if (Category == UEdGraphSchema_K2::PC_Enum) return TEXT("Enum");

	if (Category == UEdGraphSchema_K2::PC_Struct)
	{
		UScriptStruct* Struct = Cast<UScriptStruct>(Pin->PinType.PinSubCategoryObject.Get());
		if (Struct)
		{
			if (Struct == TBaseStructure<FVector>::Get()) return TEXT("Vector");
			if (Struct == TBaseStructure<FRotator>::Get()) return TEXT("Rotator");
			if (Struct == TBaseStructure<FTransform>::Get()) return TEXT("Transform");
		}
		return TEXT("Struct");
	}

	return TEXT("Wildcard");
}

void FBlueprintSerializer::ClearMappings()
{
	NodeMap.Empty();
	PinMap.Empty();
}
