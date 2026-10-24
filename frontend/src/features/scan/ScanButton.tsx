import { Alert, Button, Stack, Text } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconPlus, IconScan } from '@tabler/icons-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { getErrorMessage, isApiError } from '@/api/client'
import { usePermission } from '@/features/auth/authContext'
import { Permissions } from '@/features/auth/permissions'
import {
  createProductSuggestion,
  lookupScan,
  newProductWithBarcodePath,
  scanResultPath,
  type CreateProductSuggestion,
} from '@/features/scan/scanApi'
import { ScanDialog } from '@/features/scan/ScanDialog'

interface Failure {
  input: string
  message: string
  suggestion: CreateProductSuggestion | null
}

/** Top bar "Tara" button: scan anything and open its detail page. */
export function ScanButton() {
  const navigate = useNavigate()
  const canManageCatalog = usePermission(Permissions.CanManageCatalog)
  const [opened, { open, close }] = useDisclosure()
  const [failure, setFailure] = useState<Failure | null>(null)

  const closeDialog = () => {
    setFailure(null)
    close()
  }

  const onScan = async (value: string) => {
    setFailure(null)
    try {
      const result = await lookupScan(value)
      closeDialog()
      navigate(scanResultPath(result))
    } catch (error) {
      setFailure({
        input: value,
        message: isApiError(error, 404) ? 'Kayıt bulunamadı' : getErrorMessage(error),
        suggestion: createProductSuggestion(error),
      })
    }
  }

  const createProduct = (suggestion: CreateProductSuggestion) => {
    closeDialog()
    navigate(newProductWithBarcodePath(suggestion))
  }

  return (
    <>
      <Button variant="light" size="compact-md" leftSection={<IconScan size={18} />} onClick={open}>
        Tara
      </Button>
      <ScanDialog opened={opened} onClose={closeDialog} onScan={onScan}>
        {failure && (
          <Alert color="orange" title={failure.message}>
            <Stack gap="xs">
              <Text size="sm" ff="monospace">
                {failure.input}
              </Text>
              {failure.suggestion && canManageCatalog && (
                <Button
                  size="xs"
                  variant="light"
                  leftSection={<IconPlus size={14} />}
                  w="fit-content"
                  onClick={() => createProduct(failure.suggestion!)}
                >
                  Bu barkodla yeni ürün oluştur
                </Button>
              )}
            </Stack>
          </Alert>
        )}
      </ScanDialog>
    </>
  )
}
