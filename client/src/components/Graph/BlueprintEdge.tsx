import { memo } from 'react';
import { BezierEdge, type EdgeProps } from '@xyflow/react';
import type { PinType } from '../../types/blueprint';
import { EDGE_COLORS } from '../../constants/colors';

interface BlueprintEdgeData {
  pinType?: PinType;
  [key: string]: unknown;
}

function BlueprintEdgeComponent(props: EdgeProps<BlueprintEdgeData>) {
  const pinType = props.data?.pinType || 'Exec';
  const color = EDGE_COLORS[pinType] || '#808080';
  const isExec = pinType === 'Exec';

  return (
    <BezierEdge
      {...props}
      style={{
        stroke: color,
        strokeWidth: isExec ? 3 : 2,
        ...props.style,
      }}
    />
  );
}

export default memo(BlueprintEdgeComponent);
