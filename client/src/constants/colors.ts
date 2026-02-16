import type { NodeStyle, PinType } from '../types/blueprint';

export const NODE_HEADER_COLORS: Record<NodeStyle, string> = {
  Event: '#8C1A1A',
  Function: '#1B3A8C',
  Pure: '#1A6B3A',
  FlowControl: '#4A4A4A',
  Variable: '#2D6B4F',
  Macro: '#6B4A8C',
  Comment: '#3A3A3A',
};

export const PIN_COLORS: Record<PinType, string> = {
  Exec: '#FFFFFF',
  Bool: '#8C0000',
  Int: '#1FC4A8',
  Float: '#9ACD32',
  String: '#F0A0F0',
  Vector: '#FFC800',
  Rotator: '#9999FF',
  Transform: '#F07830',
  Object: '#0080FF',
  Class: '#8000FF',
  Byte: '#006464',
  Name: '#B0A0D0',
  Text: '#FF8080',
  Enum: '#006464',
  Struct: '#0080FF',
  Array: '#FFFFFF',
  Set: '#FFFFFF',
  Map: '#FFFFFF',
  Delegate: '#FF3030',
  Wildcard: '#808080',
};

export const EDGE_COLORS: Record<PinType, string> = {
  ...PIN_COLORS,
  Exec: '#E8E8E8',
};
