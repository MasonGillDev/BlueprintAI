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
#include "GameFramework/Actor.h"
#include "UObject/UObjectIterator.h"

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

	// Clear the pin name map for this sync
	PinNameMap.Empty();

	// Create member variables from JSON (must happen before node creation so Get/Set nodes can resolve)
	const TArray<TSharedPtr<FJsonValue>>* VariablesArray;
	if (JsonState->TryGetArrayField(TEXT("variables"), VariablesArray))
	{
		CreateVariablesFromJson(Blueprint, *VariablesArray);
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
			UEdGraphNode* NewNode = CreateNodeFromJson(Blueprint, EventGraph, NodeJson);
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
		WireConnections(EventGraph, *ConnectionsArray, NodeMap, PinNameMap);
	}

	// Compile the blueprint
	FBlueprintEditorUtils::MarkBlueprintAsModified(Blueprint);
	FKismetEditorUtilities::CompileBlueprint(Blueprint);

	UE_LOG(LogTemp, Log, TEXT("BlueprintAIBridge: Applied full sync to %s (%d nodes)"),
		*Blueprint->GetName(), NodeMap.Num());

	return true;
}

UEdGraphNode* FBlueprintDeserializer::CreateNodeFromJson(UBlueprint* Blueprint, UEdGraph* Graph, const TSharedPtr<FJsonObject>& NodeJson)
{
	FString NodeId = NodeJson->GetStringField(TEXT("id"));
	FString Title = NodeJson->GetStringField(TEXT("title"));
	FString Style = NodeJson->GetStringField(TEXT("style"));
	int32 PosX = static_cast<int32>(NodeJson->GetNumberField(TEXT("positionX")));
	int32 PosY = static_cast<int32>(NodeJson->GetNumberField(TEXT("positionY")));

	// Build PinNameMap from inputPins and outputPins arrays
	TMap<FString, FString>& NodePinMap = PinNameMap.FindOrAdd(NodeId);
	const TArray<TSharedPtr<FJsonValue>>* InputPinsArray;
	if (NodeJson->TryGetArrayField(TEXT("inputPins"), InputPinsArray))
	{
		for (const TSharedPtr<FJsonValue>& PinVal : *InputPinsArray)
		{
			TSharedPtr<FJsonObject> PinJson = PinVal->AsObject();
			if (!PinJson.IsValid()) continue;
			FString PinId = PinJson->GetStringField(TEXT("id"));
			FString PinName = PinJson->GetStringField(TEXT("name"));
			NodePinMap.Add(PinId, PinName);
		}
	}
	const TArray<TSharedPtr<FJsonValue>>* OutputPinsArray;
	if (NodeJson->TryGetArrayField(TEXT("outputPins"), OutputPinsArray))
	{
		for (const TSharedPtr<FJsonValue>& PinVal : *OutputPinsArray)
		{
			TSharedPtr<FJsonObject> PinJson = PinVal->AsObject();
			if (!PinJson.IsValid()) continue;
			FString PinId = PinJson->GetStringField(TEXT("id"));
			FString PinName = PinJson->GetStringField(TEXT("name"));
			NodePinMap.Add(PinId, PinName);
		}
	}

	// Create the appropriate node type based on style
	UEdGraphNode* NewNode = nullptr;

	if (Style == TEXT("Event"))
	{
		NewNode = CreateEventNode(Blueprint, Graph, Title, PosX, PosY);
	}
	else if (Style == TEXT("FlowControl"))
	{
		NewNode = CreateFlowControlNode(Graph, Title, PosX, PosY);
	}
	else if (Style == TEXT("Pure"))
	{
		NewNode = CreatePureNode(Graph, Title, PosX, PosY);
	}
	else if (Style == TEXT("Variable"))
	{
		NewNode = CreateVariableNode(Blueprint, Graph, Title, NodeJson, PosX, PosY);
	}
	else // "Function", "Macro", or anything else
	{
		NewNode = CreateFunctionNode(Graph, Title, PosX, PosY);
	}

	if (NewNode)
	{
		UE_LOG(LogTemp, Log, TEXT("BlueprintAIBridge: Created node '%s' (style=%s)"), *Title, *Style);
	}
	else
	{
		UE_LOG(LogTemp, Warning, TEXT("BlueprintAIBridge: Failed to create node '%s' (style=%s)"), *Title, *Style);
	}

	return NewNode;
}

UEdGraphNode* FBlueprintDeserializer::CreateEventNode(UBlueprint* Blueprint, UEdGraph* Graph, const FString& Title, int32 PosX, int32 PosY)
{
	// Parse event name by stripping "Event " prefix
	FString EventName = Title;
	if (EventName.StartsWith(TEXT("Event ")))
	{
		EventName = EventName.RightChop(6); // Remove "Event "
	}

	// Map common event names to their UFunction Receive* names
	FString FuncName;
	if (EventName == TEXT("BeginPlay"))
	{
		FuncName = TEXT("ReceiveBeginPlay");
	}
	else if (EventName == TEXT("Tick"))
	{
		FuncName = TEXT("ReceiveTick");
	}
	else if (EventName == TEXT("ActorBeginOverlap"))
	{
		FuncName = TEXT("ReceiveActorBeginOverlap");
	}
	else if (EventName == TEXT("ActorEndOverlap"))
	{
		FuncName = TEXT("ReceiveActorEndOverlap");
	}
	else
	{
		// Try Receive + EventName
		FuncName = TEXT("Receive") + EventName;
	}

	// Try to find the function on AActor
	UClass* ActorClass = AActor::StaticClass();
	UFunction* EventFunc = ActorClass->FindFunctionByName(FName(*FuncName));

	// If not found with Receive prefix, try the parent blueprint class
	if (!EventFunc && Blueprint->ParentClass)
	{
		EventFunc = Blueprint->ParentClass->FindFunctionByName(FName(*FuncName));
	}

	// If still not found, try searching by display name
	if (!EventFunc)
	{
		EventFunc = FindFunctionByDisplayName(EventName);
	}

	if (EventFunc)
	{
		// Create a standard UK2Node_Event
		UK2Node_Event* EventNode = NewObject<UK2Node_Event>(Graph);
		EventNode->CreateNewGuid();
		EventNode->EventReference.SetExternalMember(EventFunc->GetFName(), EventFunc->GetOuterUClass());
		EventNode->bOverrideFunction = true;
		EventNode->PostPlacedNewNode();
		EventNode->NodePosX = PosX;
		EventNode->NodePosY = PosY;
		Graph->AddNode(EventNode, false, false);
		EventNode->AllocateDefaultPins();
		return EventNode;
	}
	else
	{
		// Fall back to custom event
		UE_LOG(LogTemp, Warning, TEXT("BlueprintAIBridge: Could not find event function '%s', creating custom event"), *FuncName);
		UK2Node_CustomEvent* CustomNode = NewObject<UK2Node_CustomEvent>(Graph);
		CustomNode->CreateNewGuid();
		CustomNode->CustomFunctionName = FName(*EventName);
		CustomNode->PostPlacedNewNode();
		CustomNode->NodePosX = PosX;
		CustomNode->NodePosY = PosY;
		Graph->AddNode(CustomNode, false, false);
		CustomNode->AllocateDefaultPins();
		return CustomNode;
	}
}

UEdGraphNode* FBlueprintDeserializer::CreateFunctionNode(UEdGraph* Graph, const FString& Title, int32 PosX, int32 PosY)
{
	UFunction* Func = FindFunctionByDisplayName(Title);

	UK2Node_CallFunction* FuncNode = NewObject<UK2Node_CallFunction>(Graph);
	FuncNode->CreateNewGuid();
	FuncNode->PostPlacedNewNode();
	FuncNode->NodePosX = PosX;
	FuncNode->NodePosY = PosY;

	if (Func)
	{
		FuncNode->FunctionReference.SetExternalMember(Func->GetFName(), Func->GetOuterUClass());
	}
	else
	{
		UE_LOG(LogTemp, Warning, TEXT("BlueprintAIBridge: Could not find function '%s', node will have no pins"), *Title);
		FuncNode->NodeComment = Title;
	}

	Graph->AddNode(FuncNode, false, false);
	FuncNode->AllocateDefaultPins();

	return FuncNode;
}

UEdGraphNode* FBlueprintDeserializer::CreateFlowControlNode(UEdGraph* Graph, const FString& Title, int32 PosX, int32 PosY)
{
	if (Title == TEXT("Branch"))
	{
		UK2Node_IfThenElse* BranchNode = NewObject<UK2Node_IfThenElse>(Graph);
		BranchNode->CreateNewGuid();
		BranchNode->PostPlacedNewNode();
		BranchNode->NodePosX = PosX;
		BranchNode->NodePosY = PosY;
		Graph->AddNode(BranchNode, false, false);
		BranchNode->AllocateDefaultPins();
		return BranchNode;
	}
	else if (Title == TEXT("Sequence"))
	{
		UK2Node_ExecutionSequence* SeqNode = NewObject<UK2Node_ExecutionSequence>(Graph);
		SeqNode->CreateNewGuid();
		SeqNode->PostPlacedNewNode();
		SeqNode->NodePosX = PosX;
		SeqNode->NodePosY = PosY;
		Graph->AddNode(SeqNode, false, false);
		SeqNode->AllocateDefaultPins();
		return SeqNode;
	}
	else
	{
		// Fall back to function search for other flow control nodes
		UE_LOG(LogTemp, Log, TEXT("BlueprintAIBridge: FlowControl '%s' not recognized, falling back to function search"), *Title);
		return CreateFunctionNode(Graph, Title, PosX, PosY);
	}
}

UEdGraphNode* FBlueprintDeserializer::CreatePureNode(UEdGraph* Graph, const FString& Title, int32 PosX, int32 PosY)
{
	// Pure nodes are still CallFunction nodes, just with pure functions
	return CreateFunctionNode(Graph, Title, PosX, PosY);
}

bool FBlueprintDeserializer::WireConnections(UEdGraph* Graph,
	const TArray<TSharedPtr<FJsonValue>>& Connections,
	const TMap<FString, UEdGraphNode*>& NodeMap,
	const TMap<FString, TMap<FString, FString>>& PinIdToNameMap)
{
	int32 WiredCount = 0;
	int32 FailedCount = 0;

	for (const TSharedPtr<FJsonValue>& ConnVal : Connections)
	{
		TSharedPtr<FJsonObject> ConnJson = ConnVal->AsObject();
		if (!ConnJson.IsValid()) continue;

		FString SourceNodeId = ConnJson->GetStringField(TEXT("sourceNodeId"));
		FString TargetNodeId = ConnJson->GetStringField(TEXT("targetNodeId"));
		FString SourcePinId = ConnJson->GetStringField(TEXT("sourcePinId"));
		FString TargetPinId = ConnJson->GetStringField(TEXT("targetPinId"));
		FString PinType = ConnJson->GetStringField(TEXT("pinType"));

		UEdGraphNode* const* SourceNodePtr = NodeMap.Find(SourceNodeId);
		UEdGraphNode* const* TargetNodePtr = NodeMap.Find(TargetNodeId);

		if (!SourceNodePtr || !TargetNodePtr)
		{
			UE_LOG(LogTemp, Warning, TEXT("BlueprintAIBridge: Connection references missing node (source=%s, target=%s)"),
				*SourceNodeId, *TargetNodeId);
			FailedCount++;
			continue;
		}

		// Resolve pin names from the PinIdToNameMap
		FString SourcePinName;
		FString TargetPinName;

		const TMap<FString, FString>* SourcePinMap = PinIdToNameMap.Find(SourceNodeId);
		if (SourcePinMap)
		{
			const FString* Name = SourcePinMap->Find(SourcePinId);
			if (Name) SourcePinName = *Name;
		}

		const TMap<FString, FString>* TargetPinMap = PinIdToNameMap.Find(TargetNodeId);
		if (TargetPinMap)
		{
			const FString* Name = TargetPinMap->Find(TargetPinId);
			if (Name) TargetPinName = *Name;
		}

		UEdGraphPin* SourcePin = nullptr;
		UEdGraphPin* TargetPin = nullptr;

		// Try to find pins by resolved display name
		if (!SourcePinName.IsEmpty())
		{
			SourcePin = FindPinByName(*SourceNodePtr, SourcePinName, EGPD_Output);
		}
		if (!TargetPinName.IsEmpty())
		{
			TargetPin = FindPinByName(*TargetNodePtr, TargetPinName, EGPD_Input);
		}

		// Fallback for exec pins: if pin name is empty or not found, try matching by exec type
		if (!SourcePin && PinType == TEXT("Exec"))
		{
			for (UEdGraphPin* Pin : (*SourceNodePtr)->Pins)
			{
				if (Pin->Direction == EGPD_Output && Pin->PinType.PinCategory == UEdGraphSchema_K2::PC_Exec)
				{
					SourcePin = Pin;
					break;
				}
			}
		}
		if (!TargetPin && PinType == TEXT("Exec"))
		{
			for (UEdGraphPin* Pin : (*TargetNodePtr)->Pins)
			{
				if (Pin->Direction == EGPD_Input && Pin->PinType.PinCategory == UEdGraphSchema_K2::PC_Exec)
				{
					TargetPin = Pin;
					break;
				}
			}
		}

		if (SourcePin && TargetPin)
		{
			SourcePin->MakeLinkTo(TargetPin);
			WiredCount++;
		}
		else
		{
			UE_LOG(LogTemp, Warning, TEXT("BlueprintAIBridge: Failed to wire connection (srcNode=%s, srcPin=%s [%s], tgtNode=%s, tgtPin=%s [%s])"),
				*SourceNodeId, *SourcePinId, *SourcePinName, *TargetNodeId, *TargetPinId, *TargetPinName);
			FailedCount++;
		}
	}

	UE_LOG(LogTemp, Log, TEXT("BlueprintAIBridge: Wired %d connections (%d failed)"), WiredCount, FailedCount);
	return WiredCount > 0;
}

UEdGraphPin* FBlueprintDeserializer::FindPinByName(UEdGraphNode* Node, const FString& PinName, EEdGraphPinDirection Direction)
{
	if (!Node) return nullptr;

	// First try: match by display name
	for (UEdGraphPin* Pin : Node->Pins)
	{
		if (Pin->Direction == Direction && Pin->GetDisplayName().ToString().Equals(PinName, ESearchCase::IgnoreCase))
		{
			return Pin;
		}
	}

	// Second try: match by PinName field (internal name)
	for (UEdGraphPin* Pin : Node->Pins)
	{
		if (Pin->Direction == Direction && Pin->PinName.ToString().Equals(PinName, ESearchCase::IgnoreCase))
		{
			return Pin;
		}
	}

	return nullptr;
}

UFunction* FBlueprintDeserializer::FindFunctionByDisplayName(const FString& DisplayName)
{
	// Check cache first
	if (UFunction** CachedFunc = FunctionCache.Find(DisplayName))
	{
		return *CachedFunc;
	}

	// Search across all loaded classes
	for (TObjectIterator<UClass> ClassIt; ClassIt; ++ClassIt)
	{
		UClass* Class = *ClassIt;
		for (TFieldIterator<UFunction> FuncIt(Class, EFieldIteratorFlags::ExcludeSuper); FuncIt; ++FuncIt)
		{
			UFunction* Func = *FuncIt;
			if (Func->GetDisplayNameText().ToString() == DisplayName)
			{
				FunctionCache.Add(DisplayName, Func);
				UE_LOG(LogTemp, Log, TEXT("BlueprintAIBridge: Resolved function '%s' → %s::%s"),
					*DisplayName, *Class->GetName(), *Func->GetName());
				return Func;
			}
		}
	}

	UE_LOG(LogTemp, Warning, TEXT("BlueprintAIBridge: Could not find UFunction with display name '%s'"), *DisplayName);
	FunctionCache.Add(DisplayName, nullptr);
	return nullptr;
}

FEdGraphPinType FBlueprintDeserializer::MapPinTypeFromString(const FString& TypeStr)
{
	FEdGraphPinType PinType;
	PinType.PinCategory = UEdGraphSchema_K2::PC_Wildcard; // default fallback

	if (TypeStr == TEXT("Bool"))
	{
		PinType.PinCategory = UEdGraphSchema_K2::PC_Boolean;
	}
	else if (TypeStr == TEXT("Int"))
	{
		PinType.PinCategory = UEdGraphSchema_K2::PC_Int;
	}
	else if (TypeStr == TEXT("Float"))
	{
		PinType.PinCategory = UEdGraphSchema_K2::PC_Real;
	}
	else if (TypeStr == TEXT("String"))
	{
		PinType.PinCategory = UEdGraphSchema_K2::PC_String;
	}
	else if (TypeStr == TEXT("Name"))
	{
		PinType.PinCategory = UEdGraphSchema_K2::PC_Name;
	}
	else if (TypeStr == TEXT("Text"))
	{
		PinType.PinCategory = UEdGraphSchema_K2::PC_Text;
	}
	else if (TypeStr == TEXT("Byte"))
	{
		PinType.PinCategory = UEdGraphSchema_K2::PC_Byte;
	}
	else if (TypeStr == TEXT("Object"))
	{
		PinType.PinCategory = UEdGraphSchema_K2::PC_Object;
	}
	else if (TypeStr == TEXT("Vector"))
	{
		PinType.PinCategory = UEdGraphSchema_K2::PC_Struct;
		PinType.PinSubCategoryObject = TBaseStructure<FVector>::Get();
	}
	else if (TypeStr == TEXT("Rotator"))
	{
		PinType.PinCategory = UEdGraphSchema_K2::PC_Struct;
		PinType.PinSubCategoryObject = TBaseStructure<FRotator>::Get();
	}
	else if (TypeStr == TEXT("Transform"))
	{
		PinType.PinCategory = UEdGraphSchema_K2::PC_Struct;
		PinType.PinSubCategoryObject = TBaseStructure<FTransform>::Get();
	}

	return PinType;
}

void FBlueprintDeserializer::CreateVariablesFromJson(UBlueprint* Blueprint, const TArray<TSharedPtr<FJsonValue>>& VariablesArray)
{
	// Clear existing user-defined variables (NewVariables), preserving system variables
	Blueprint->NewVariables.Empty();

	for (const TSharedPtr<FJsonValue>& VarVal : VariablesArray)
	{
		TSharedPtr<FJsonObject> VarJson = VarVal->AsObject();
		if (!VarJson.IsValid()) continue;

		FString Name = VarJson->GetStringField(TEXT("name"));
		FString TypeStr = VarJson->GetStringField(TEXT("type"));

		FEdGraphPinType PinType = MapPinTypeFromString(TypeStr);

		// Add the member variable
		bool bSuccess = FBlueprintEditorUtils::AddMemberVariable(Blueprint, FName(*Name), PinType);
		if (!bSuccess)
		{
			UE_LOG(LogTemp, Warning, TEXT("BlueprintAIBridge: Failed to add variable '%s'"), *Name);
			continue;
		}

		// Find the newly created variable description and set properties
		for (FBPVariableDescription& VarDesc : Blueprint->NewVariables)
		{
			if (VarDesc.VarName == FName(*Name))
			{
				// Set default value
				FString DefaultValue;
				if (VarJson->TryGetStringField(TEXT("defaultValue"), DefaultValue) && !DefaultValue.IsEmpty())
				{
					VarDesc.DefaultValue = DefaultValue;
				}

				// Set category
				FString Category;
				if (VarJson->TryGetStringField(TEXT("category"), Category) && !Category.IsEmpty())
				{
					VarDesc.Category = FText::FromString(Category);
				}

				// Set editability flags
				bool bIsEditable = false;
				if (VarJson->TryGetBoolField(TEXT("isEditable"), bIsEditable) && bIsEditable)
				{
					VarDesc.PropertyFlags |= CPF_Edit | CPF_BlueprintVisible;
				}

				break;
			}
		}

		UE_LOG(LogTemp, Log, TEXT("BlueprintAIBridge: Created variable '%s' (type=%s)"), *Name, *TypeStr);
	}
}

UEdGraphNode* FBlueprintDeserializer::CreateVariableNode(UBlueprint* Blueprint, UEdGraph* Graph, const FString& Title, const TSharedPtr<FJsonObject>& NodeJson, int32 PosX, int32 PosY)
{
	// Determine if this is a Get or Set node, and extract the variable name
	bool bIsSetter = false;
	FString VarName = Title;

	if (Title.StartsWith(TEXT("Set ")))
	{
		bIsSetter = true;
		VarName = Title.RightChop(4);
	}
	else if (Title.StartsWith(TEXT("Get ")))
	{
		VarName = Title.RightChop(4);
	}

	// Ensure the skeleton class is up to date so we can find the property
	FBlueprintEditorUtils::MarkBlueprintAsStructurallyModified(Blueprint);

	if (bIsSetter)
	{
		UK2Node_VariableSet* SetNode = NewObject<UK2Node_VariableSet>(Graph);
		SetNode->CreateNewGuid();
		SetNode->VariableReference.SetSelfMember(FName(*VarName));
		SetNode->PostPlacedNewNode();
		SetNode->NodePosX = PosX;
		SetNode->NodePosY = PosY;
		Graph->AddNode(SetNode, false, false);
		SetNode->AllocateDefaultPins();
		return SetNode;
	}
	else
	{
		UK2Node_VariableGet* GetNode = NewObject<UK2Node_VariableGet>(Graph);
		GetNode->CreateNewGuid();
		GetNode->VariableReference.SetSelfMember(FName(*VarName));
		GetNode->PostPlacedNewNode();
		GetNode->NodePosX = PosX;
		GetNode->NodePosY = PosY;
		Graph->AddNode(GetNode, false, false);
		GetNode->AllocateDefaultPins();
		return GetNode;
	}
}
