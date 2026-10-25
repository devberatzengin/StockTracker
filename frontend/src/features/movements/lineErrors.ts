import { getErrorMessage, isApiError } from '@/api/client'

export interface LineErrors {
  /** Errors per line index and field, e.g. lines[2].quantity → { 2: { quantity: '…' } }. */
  lines: Record<number, Record<string, string>>
  /** Errors not tied to a line (or the whole message when there are no field errors). */
  general: string[]
  /** Errors for top-level fields such as toLocationId or note. */
  fields: Record<string, string>
}

const LINE_KEY = /^lines\[(\d+)\]\.?(.*)$/

/** Splits ProblemDetails `errors` into per-line, per-field and general messages. */
export function parseLineErrors(error: unknown): LineErrors {
  const result: LineErrors = { lines: {}, general: [], fields: {} }
  if (!isApiError(error) || !error.errors || Object.keys(error.errors).length === 0) {
    result.general.push(getErrorMessage(error))
    return result
  }

  for (const [key, messages] of Object.entries(error.errors)) {
    const message = messages[0]
    const match = LINE_KEY.exec(key)
    if (match) {
      const index = Number(match[1])
      result.lines[index] = { ...result.lines[index], [match[2] || 'line']: message }
    } else if (key === 'lines') {
      result.general.push(message)
    } else {
      result.fields[key] = message
    }
  }

  if (Object.keys(result.lines).length > 0 && result.general.length === 0) {
    result.general.push(
      'Bazı satırlarda hata var; işaretli satırları düzeltin. Hiçbir hareket kaydedilmedi.',
    )
  }
  return result
}

/** First message of a line, whatever field it is on. */
export const lineMessage = (errors: LineErrors, index: number) => {
  const line = errors.lines[index]
  return line ? Object.values(line)[0] : undefined
}
