import { z } from 'zod'

/** Mirrors the backend Identity policy: 8+ characters with upper case, lower case and a digit. */
export const passwordSchema = z
  .string()
  .min(8, 'Şifre en az 8 karakter olmalıdır.')
  .regex(/[A-Z]/, 'Şifre en az bir büyük harf içermelidir.')
  .regex(/[a-z]/, 'Şifre en az bir küçük harf içermelidir.')
  .regex(/[0-9]/, 'Şifre en az bir rakam içermelidir.')
