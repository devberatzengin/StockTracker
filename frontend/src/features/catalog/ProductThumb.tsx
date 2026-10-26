import { Center, Image } from '@mantine/core'
import { IconPhoto } from '@tabler/icons-react'

/** Small square product thumbnail with a placeholder when there is no photo. */
export function ProductThumb({
  url,
  size = 36,
}: {
  url: string | null | undefined
  size?: number
}) {
  return url ? (
    <Image src={url} alt="" w={size} h={size} fit="cover" radius="sm" />
  ) : (
    <Center w={size} h={size} bg="var(--mantine-color-default-hover)" style={{ borderRadius: 4 }}>
      <IconPhoto size={size / 2} color="var(--mantine-color-dimmed)" />
    </Center>
  )
}
