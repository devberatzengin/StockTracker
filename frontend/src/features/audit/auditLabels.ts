import type { AuditAction } from '@/api/types'
import { TRACKING_TYPE_LABELS, UNIT_LABELS } from '@/features/catalog/labels'
import { LOCATION_TYPE_LABELS } from '@/features/locations/labels'
import { ITEM_STATUS_LABELS } from '@/features/movements/labels'
import { ROLE_LABELS } from '@/features/users/roles'
import { formatDateTime } from '@/lib/dates'

export const ENTITY_TYPE_LABELS: Record<string, string> = {
  Product: 'Ürün',
  ProductBarcode: 'Barkod',
  ProductPhoto: 'Fotoğraf',
  Category: 'Kategori',
  Item: 'Adet',
  Location: 'Lokasyon',
  User: 'Kullanıcı',
  Assignment: 'Zimmet',
  CountSession: 'Sayım',
}

export const ENTITY_TYPE_OPTIONS = Object.entries(ENTITY_TYPE_LABELS).map(([value, label]) => ({
  value,
  label,
}))

export const AUDIT_ACTION_LABELS: Record<AuditAction, string> = {
  Created: 'Oluşturuldu',
  Updated: 'Güncellendi',
  Deactivated: 'Pasifleştirildi',
  Reactivated: 'Aktifleştirildi',
  Deleted: 'Silindi',
}

export const AUDIT_ACTION_COLORS: Record<AuditAction, string> = {
  Created: 'green',
  Updated: 'blue',
  Deactivated: 'gray',
  Reactivated: 'teal',
  Deleted: 'red',
}

const FIELD_LABELS: Record<string, string> = {
  name: 'Ad',
  code: 'Kod',
  description: 'Açıklama',
  categoryId: 'Kategori',
  unit: 'Birim',
  trackingType: 'Takip tipi',
  isAssignable: 'Zimmetlenebilir',
  minStockLevel: 'Minimum stok',
  isActive: 'Aktif',
  value: 'Barkod',
  format: 'Barkod biçimi',
  serialNumber: 'Seri no',
  status: 'Durum',
  currentLocationId: 'Konum',
  productId: 'Ürün',
  parentId: 'Üst lokasyon',
  type: 'Tip',
  fullName: 'Ad soyad',
  department: 'Departman',
  email: 'E-posta',
  role: 'Rol',
  canLogin: 'Giriş yetkisi',
  sortOrder: 'Sıra',
  isPrimary: 'Ana fotoğraf',
  sizeBytes: 'Boyut',
  assigneeUserId: 'Alan kişi',
  dueDate: 'Teslim tarihi',
  note: 'Not',
  returnedAt: 'İade tarihi',
  returnCondition: 'İade durumu',
  returnNote: 'İade notu',
  includeChildren: 'Alt lokasyonlar dahil',
  approvedAt: 'Onay tarihi',
}

// Technical fields that mean nothing on screen (materialized paths, storage keys, references).
const HIDDEN_FIELDS = new Set([
  'path',
  'depth',
  'storageKey',
  'thumbnailKey',
  'contentType',
  'itemId',
  'countSessionId',
  'assignReferenceId',
  'returnReferenceId',
  'approvalReferenceId',
  'assignedByUserId',
  'assignedAt',
  'returnedByUserId',
  'returnLocationId',
  'startedBy',
  'startedAt',
  'approvedBy',
  'locationId',
])

const VALUE_LABELS: Record<string, Record<string, string>> = {
  unit: UNIT_LABELS,
  trackingType: TRACKING_TYPE_LABELS,
  status: {
    ...ITEM_STATUS_LABELS,
    Counting: 'Sayılıyor',
    Reviewing: 'İncelemede',
    Approved: 'Onaylandı',
    Cancelled: 'İptal edildi',
  },
  role: ROLE_LABELS,
  type: LOCATION_TYPE_LABELS,
  returnCondition: { Good: 'Sağlam', Faulty: 'Arızalı' },
}

export const isVisibleField = (field: string) => !HIDDEN_FIELDS.has(field)

export const fieldLabel = (field: string) => FIELD_LABELS[field] ?? field

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const ISO_DATE_TIME = /^\d{4}-\d{2}-\d{2}T/

/** Old/new value in Turkish: enums by label, booleans as Evet/Hayır, ids shortened. */
export function formatAuditValue(field: string, value: string | null | undefined): string {
  if (value === null || value === undefined || value === '') return '—'
  if (value === 'true') return 'Evet'
  if (value === 'false') return 'Hayır'
  const label = VALUE_LABELS[field]?.[value]
  if (label) return label
  if (GUID.test(value)) return `#${value.slice(0, 8)}`
  if (ISO_DATE_TIME.test(value)) return formatDateTime(value)
  if (field === 'sizeBytes') return `${Math.round(Number(value) / 1024)} KB`
  return value
}
