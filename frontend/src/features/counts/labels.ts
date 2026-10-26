import type { CountLineState, CountResolution, CountStatus } from '@/api/types'

export const COUNT_STATUS_LABELS: Record<CountStatus, string> = {
  Counting: 'Sayılıyor',
  Reviewing: 'İncelemede',
  Approved: 'Onaylandı',
  Cancelled: 'İptal edildi',
}

export const COUNT_STATUS_COLORS: Record<CountStatus, string> = {
  Counting: 'blue',
  Reviewing: 'orange',
  Approved: 'green',
  Cancelled: 'gray',
}

export const COUNT_LINE_STATE_LABELS: Record<CountLineState, string> = {
  Matched: 'Eşleşti',
  NotCountedEmpty: 'Sayılmadı (stok yok)',
  QuantityDifference: 'Miktar farkı',
  QuantityNotCounted: 'Hiç sayılmadı',
  ItemMissing: 'Bulunamadı',
  ItemFoundElsewhere: 'Başka rafta bulundu',
  ItemMovedDuringCount: 'Sayım sırasında hareket etti',
  UnexpectedMovable: 'Beklenmeyen adet',
  UnexpectedAlreadyHere: 'Beklenmeyen, artık burada',
  UnexpectedNoteOnly: 'Beklenmeyen (zimmetli/tamirde/hurda)',
}

export const COUNT_RESOLUTION_LABELS: Record<CountResolution, string> = {
  ApplyDifference: 'Farkı uygula',
  AcceptZero: '0 kabul et',
  OutOfScope: 'Kapsam dışı',
  MarkLost: 'Kayıp olarak çıkar',
  MoveHere: 'Buraya taşı',
  Ignore: 'Dokunma',
}
