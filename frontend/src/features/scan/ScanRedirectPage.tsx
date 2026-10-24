import { Alert, Button, Center, Loader, Stack } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { IconPlus } from '@tabler/icons-react'
import { Link, Navigate, useParams } from 'react-router'
import { getErrorMessage, isApiError } from '@/api/client'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import {
  createProductSuggestion,
  lookupScan,
  newProductWithBarcodePath,
  scanResultPath,
} from '@/features/scan/scanApi'

/**
 * Target of QR label URLs ({PUBLIC_BASE_URL}/s/{code}), so the phone's own camera app works too.
 * Sits behind ProtectedRoute: signed-out users log in first and come back here.
 */
export function ScanRedirectPage() {
  const { code = '' } = useParams()
  const canManageCatalog = usePermission(Permissions.CanManageCatalog)
  const { data, error, isPending } = useQuery({
    queryKey: ['scan', code],
    queryFn: () => lookupScan(code),
    meta: { skipGlobalError: true },
  })

  if (data) {
    return <Navigate to={scanResultPath(data)} replace />
  }

  if (isPending) {
    return (
      <Center py="xl">
        <Loader aria-label="Kod çözümleniyor" />
      </Center>
    )
  }

  const notFound = isApiError(error, 404)
  const suggestion = createProductSuggestion(error)
  return (
    <Stack maw={480}>
      <Alert color="orange" title={notFound ? 'Kayıt bulunamadı' : 'Kod açılamadı'}>
        {notFound ? `"${code}" koduna ait kayıt yok.` : getErrorMessage(error)}
      </Alert>
      {suggestion && canManageCatalog && (
        <Button
          component={Link}
          to={newProductWithBarcodePath(suggestion)}
          leftSection={<IconPlus size={16} />}
          w="fit-content"
        >
          Bu barkodla yeni ürün oluştur
        </Button>
      )}
      <Button component={Link} to="/" variant="subtle" w="fit-content">
        Ana sayfaya dön
      </Button>
    </Stack>
  )
}
