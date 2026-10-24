/** Policy names; must match StockTracker.Application.Common.Security.Permissions. */
export const Permissions = {
  CanManageUsers: 'CanManageUsers',
  CanManageCatalog: 'CanManageCatalog',
  CanMoveStock: 'CanMoveStock',
  CanApproveCount: 'CanApproveCount',
  CanViewReports: 'CanViewReports',
  CanViewAudit: 'CanViewAudit',
} as const

export type Permission = (typeof Permissions)[keyof typeof Permissions]
