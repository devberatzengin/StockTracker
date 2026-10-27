import type { LabelTemplate } from '@/api/types'

export interface TemplateInfo {
  label: string
  description: string
  columns: number
  rows: number
}

/** Same geometry as the server's label sheets (PLAN 3.3). */
export const LABEL_TEMPLATES: Record<LabelTemplate, TemplateInfo> = {
  Sheet3x8: {
    label: 'A4 3×8 (70×37 mm)',
    description: 'Ürün ve adet: QR, kod, ad, seri no',
    columns: 3,
    rows: 8,
  },
  Sheet2x4: {
    label: 'A4 2×4 (105×74 mm)',
    description: 'Raf, dolap, oda: büyük ad ve üst lokasyonlar',
    columns: 2,
    rows: 4,
  },
  Sheet5x13: {
    label: 'A4 5×13 (38×21 mm)',
    description: 'Küçük ürünler: QR ve kod',
    columns: 5,
    rows: 13,
  },
  Thermal50x30: {
    label: 'Termal 50×30 mm',
    description: 'Etiket yazıcısı: her etiket ayrı sayfa',
    columns: 1,
    rows: 1,
  },
}

export const isSheet = (template: LabelTemplate) => template !== 'Thermal50x30'

/** What to print; exactly one of the fields. */
export type LabelTarget =
  | { productIds: string[] }
  | { itemIds: string[] }
  | { referenceId: string }
  | { locationId: string }
