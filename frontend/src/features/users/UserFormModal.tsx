import { zodResolver } from '@hookform/resolvers/zod'
import {
  Alert,
  Button,
  Group,
  Modal,
  PasswordInput,
  Select,
  Stack,
  Switch,
  TextInput,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { applyServerErrors } from '@/api/formErrors'
import type { UserDto } from '@/api/types'
import { passwordSchema } from '@/features/auth/passwordSchema'
import { ROLE_OPTIONS } from '@/features/users/roles'
import { useCreateUser, useUpdateUser } from '@/features/users/usersApi'

const FIELDS = ['fullName', 'department', 'email', 'password', 'role', 'canLogin'] as const

function createSchema(isEdit: boolean) {
  return z
    .object({
      fullName: z.string().trim().min(1, 'Ad soyad zorunludur.').max(100, 'En fazla 100 karakter.'),
      department: z.string().trim().max(100, 'En fazla 100 karakter.'),
      role: z.string().min(1, 'Rol seçin.'),
      canLogin: z.boolean(),
      email: z.string().trim().max(256, 'En fazla 256 karakter.'),
      password: z.string(),
    })
    .superRefine((v, ctx) => {
      if (!v.canLogin) return
      if (!v.email) {
        ctx.addIssue({ code: 'custom', path: ['email'], message: 'E-posta zorunludur.' })
      } else if (!z.email().safeParse(v.email).success) {
        ctx.addIssue({
          code: 'custom',
          path: ['email'],
          message: 'Geçerli bir e-posta adresi girin.',
        })
      }
      if (!v.password && !isEdit) {
        ctx.addIssue({ code: 'custom', path: ['password'], message: 'Şifre zorunludur.' })
      } else if (v.password) {
        for (const issue of passwordSchema.safeParse(v.password).error?.issues ?? []) {
          ctx.addIssue({ code: 'custom', path: ['password'], message: issue.message })
        }
      }
    })
}

type FormValues = z.infer<ReturnType<typeof createSchema>>

function toFormValues(user?: UserDto): FormValues {
  return {
    fullName: user?.fullName ?? '',
    department: user?.department ?? '',
    role: user?.role ?? 'Staff',
    canLogin: user?.canLogin ?? true,
    email: user?.email ?? '',
    password: '',
  }
}

interface Props {
  opened: boolean
  onClose: () => void
  /** Present when editing. */
  user?: UserDto
}

export function UserFormModal({ opened, onClose, user }: Props) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={user ? 'Kullanıcıyı düzenle' : 'Yeni kullanıcı'}
    >
      {/* Modal content unmounts on close, so every open starts with fresh form state. */}
      <UserForm user={user} onClose={onClose} />
    </Modal>
  )
}

function UserForm({ user, onClose }: { user?: UserDto; onClose: () => void }) {
  const isEdit = !!user
  const createUser = useCreateUser()
  const updateUser = useUpdateUser()
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(createSchema(isEdit)),
    defaultValues: toFormValues(user),
  })

  const canLogin = useWatch({ control, name: 'canLogin' })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    const body = {
      fullName: values.fullName,
      department: values.department || null,
      role: values.role,
      canLogin: values.canLogin,
      // Hidden fields keep their value: an email stays as contact info when login is turned off.
      email: values.email || null,
      password: values.canLogin && values.password ? values.password : null,
    }

    try {
      if (user) {
        await updateUser.mutateAsync({ id: user.id, body })
      } else {
        await createUser.mutateAsync(body)
      }
      notifications.show({
        color: 'green',
        message: isEdit ? 'Kullanıcı güncellendi.' : 'Kullanıcı oluşturuldu.',
      })
      onClose()
    } catch (e) {
      setFormError(applyServerErrors(e, setError, FIELDS))
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate>
      <Stack>
        {formError && <Alert color="red">{formError}</Alert>}
        <TextInput
          label="Ad soyad"
          required
          error={errors.fullName?.message}
          {...register('fullName')}
        />
        <TextInput
          label="Departman"
          error={errors.department?.message}
          {...register('department')}
        />
        <Controller
          control={control}
          name="role"
          render={({ field }) => (
            <Select
              label="Rol"
              required
              data={ROLE_OPTIONS}
              allowDeselect={false}
              value={field.value}
              onChange={(value) => field.onChange(value ?? '')}
              error={errors.role?.message}
            />
          )}
        />
        <Controller
          control={control}
          name="canLogin"
          render={({ field }) => (
            <Switch
              label="Giriş yetkisi"
              description="Kapalıysa kullanıcı sisteme giremez; sadece zimmet alıcısı olarak bulunur."
              checked={field.value}
              onChange={(event) => field.onChange(event.currentTarget.checked)}
            />
          )}
        />
        {canLogin && (
          <>
            <TextInput
              label="E-posta"
              type="email"
              required
              autoComplete="off"
              error={errors.email?.message}
              {...register('email')}
            />
            <PasswordInput
              label="Şifre"
              required={!isEdit}
              description={
                isEdit
                  ? 'Boş bırakılırsa değişmez. Giriş yetkisi yeni açılıyorsa zorunludur.'
                  : 'En az 8 karakter; büyük harf, küçük harf ve rakam içermeli.'
              }
              autoComplete="new-password"
              error={errors.password?.message}
              {...register('password')}
            />
          </>
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Vazgeç
          </Button>
          <Button type="submit" loading={isSubmitting}>
            Kaydet
          </Button>
        </Group>
      </Stack>
    </form>
  )
}
