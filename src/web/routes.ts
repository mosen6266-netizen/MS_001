/** Local state-only navigation: does not change document, address or network. */
export const SHELL_ROUTES = [
  { id: "overview", label: "工作台", description: "项目运行状态概览", shortcut: "Alt+1" },
  { id: "accounts", label: "账号管理", description: "已授权账号列表（待后续接入）", shortcut: "Alt+2" },
  { id: "groups", label: "群组与话题", description: "群组/话题列表（待后续接入）", shortcut: "Alt+3" },
  { id: "scripts", label: "剧本编辑", description: "按备注分配角色（待后续接入）", shortcut: "Alt+4" },
  { id: "running", label: "运行中心", description: "独立群任务状态（待后续接入）", shortcut: "Alt+5" },
  { id: "settings", label: "设置", description: "本地设置与诊断（待后续接入）", shortcut: "Alt+6" },
] as const;
export type ShellRoute = (typeof SHELL_ROUTES)[number]["id"];
export type ShellState = Readonly<{ active: ShellRoute; scroll: Readonly<Record<ShellRoute, number>> }>;
export const ALL_ROUTE_IDS: readonly ShellRoute[] = SHELL_ROUTES.map(item => item.id);
export function isShellRoute(value: unknown): value is ShellRoute {
  return typeof value === "string" && ALL_ROUTE_IDS.some(id => id === value);
}
export function initialShellState(): ShellState {
  return { active: "overview", scroll: Object.fromEntries(ALL_ROUTE_IDS.map(id => [id, 0])) as Record<ShellRoute, number> };
}
export function transitionShellRoute(previous: ShellState, target: ShellRoute, currentScrollTop: number): ShellState {
  if (!isShellRoute(target)) throw new Error("Unknown destination");
  if (previous.active === target) return previous;
  const offset = Number.isFinite(currentScrollTop) ? Math.max(0, Math.floor(currentScrollTop)) : 0;
  return { active: target, scroll: { ...previous.scroll, [previous.active]: offset } };
}
export function shortcutRoute(alt: boolean, ctrl: boolean, meta: boolean, key: string): ShellRoute | null {
  if (!alt || ctrl || meta || !/^[1-6]$/.test(key)) return null;
  return SHELL_ROUTES[Number(key) - 1].id;
}
