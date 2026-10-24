export const ROLE_LABELS: Record<string, string> = {
  Admin: 'Yönetici',
  WarehouseManager: 'Depo Sorumlusu',
  Staff: 'Personel',
}

export const ROLE_OPTIONS = Object.entries(ROLE_LABELS).map(([value, label]) => ({ value, label }))

export const roleLabel = (role: string) => ROLE_LABELS[role] ?? role
