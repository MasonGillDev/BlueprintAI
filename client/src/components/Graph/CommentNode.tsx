import { memo } from 'react';
import type { NodeProps } from '@xyflow/react';

interface CommentData {
  text: string;
  width: number;
  height: number;
  color: string;
  [key: string]: unknown;
}

function CommentNodeComponent({ data }: NodeProps<CommentData>) {
  return (
    <div
      className="bp-comment"
      style={{
        width: data.width,
        height: data.height,
        borderColor: data.color + '60',
        backgroundColor: data.color + '15',
      }}
    >
      <div className="bp-comment-header" style={{ backgroundColor: data.color + '40' }}>
        {data.text}
      </div>
    </div>
  );
}

export default memo(CommentNodeComponent);
