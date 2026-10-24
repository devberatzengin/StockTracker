import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { getErrorMessage, isApiError } from '@/api/client'

/**
 * Copies backend validation errors (ProblemDetails `errors`, PascalCase keys) onto form fields.
 * Returns a message for errors that do not belong to a known field, or null when all were mapped.
 */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: readonly Path<T>[],
): string | null {
  if (!isApiError(error, 400) || !error.errors) {
    return getErrorMessage(error)
  }

  const unmapped: string[] = []
  for (const [key, messages] of Object.entries(error.errors)) {
    const field = (key.charAt(0).toLowerCase() + key.slice(1)) as Path<T>
    if (fields.includes(field)) {
      setError(field, { message: messages[0] })
    } else {
      unmapped.push(...messages)
    }
  }

  return unmapped.length > 0 ? unmapped.join(' ') : null
}
