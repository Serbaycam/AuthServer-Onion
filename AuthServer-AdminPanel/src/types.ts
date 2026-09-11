export interface User { id: string; firstName: string; lastName: string; fullName: string; email: string; isActive: boolean; roles: string[] }
export interface Role { id: string; name: string }
export interface Permission { permissionName: string }
export interface ActiveSession { tokenId: string; userEmail: string; fullName: string; ipAddress: string; createdDate: string; expirationDate: string; isCurrentSession: boolean }
export interface DashboardStats { totalUsers: number; activeUsers: number; passiveUsers: number; totalRoles: number; totalActiveSessions: number;
  latestActivities: { userEmail: string; action: string; date: string; ipAddress: string }[] }
export function formatDate(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('tr-TR');
}
