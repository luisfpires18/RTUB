/**
 * Window-level village game API exposed to Blazor via IJSRuntime.
 */
export interface VillageModeGameApi {
  start(containerId: string, netRef: DotNet.DotNetObject): Promise<void>;
  destroy(): void;
  isActive(): boolean;
}
