import { useBlueprintStore } from '../store/blueprintStore';
import { PIN_COLORS } from '../constants/colors';

export default function VariablesPanel() {
  const variables = useBlueprintStore((s) => s.variables);

  if (variables.length === 0) return null;

  return (
    <div className="variables-panel">
      <h4>Variables</h4>
      <div className="variables-list">
        {variables.map((v) => (
          <div key={v.id} className="variable-item">
            <span
              className="variable-type-dot"
              style={{ backgroundColor: PIN_COLORS[v.type] || '#808080' }}
            />
            <span className="variable-name">{v.name}</span>
            <span className="variable-type">{v.type}</span>
            {v.defaultValue && (
              <span className="variable-default">= {v.defaultValue}</span>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
