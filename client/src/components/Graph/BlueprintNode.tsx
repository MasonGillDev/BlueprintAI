import { memo } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import type { BlueprintNode as BPNode, Pin } from '../../types/blueprint';
import { NODE_HEADER_COLORS, PIN_COLORS } from '../../constants/colors';

type BlueprintNodeData = BPNode & { label?: string };

function PinHandle({ pin, position, nodeId }: { pin: Pin; position: Position; nodeId: string }) {
  const isExec = pin.type === 'Exec';
  const color = PIN_COLORS[pin.type] || '#808080';
  const handleId = pin.id;

  return (
    <div
      className={`bp-pin ${position === Position.Left ? 'bp-pin-input' : 'bp-pin-output'}`}
    >
      <Handle
        type={position === Position.Left ? 'target' : 'source'}
        position={position}
        id={handleId}
        className={`bp-handle ${isExec ? 'bp-handle-exec' : 'bp-handle-data'}`}
        style={{
          background: pin.isConnected ? color : 'transparent',
          borderColor: color,
        }}
      />
      <span
        className="bp-pin-label"
        style={{ color: '#C8C8C8' }}
      >
        {pin.name}
      </span>
      {pin.defaultValue && !pin.isConnected && (
        <span className="bp-pin-default">{pin.defaultValue}</span>
      )}
    </div>
  );
}

function BlueprintNodeComponent({ data, id }: NodeProps<BlueprintNodeData>) {
  const headerColor = NODE_HEADER_COLORS[data.style] || '#4A4A4A';
  const maxPins = Math.max(data.inputPins.length, data.outputPins.length);

  return (
    <div className="bp-node">
      <div className="bp-node-header" style={{ backgroundColor: headerColor }}>
        <span className="bp-node-title">{data.title}</span>
      </div>
      <div className="bp-node-body">
        <div className="bp-pins-container">
          <div className="bp-pins-left">
            {data.inputPins.map((pin) => (
              <PinHandle key={pin.id} pin={pin} position={Position.Left} nodeId={id} />
            ))}
          </div>
          <div className="bp-pins-right">
            {data.outputPins.map((pin) => (
              <PinHandle key={pin.id} pin={pin} position={Position.Right} nodeId={id} />
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

export default memo(BlueprintNodeComponent);
