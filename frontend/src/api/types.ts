import type { components } from '@/api/schema'

type Schemas = components['schemas']

export type AccessTokenResponse = Schemas['AccessTokenResponse']
export type CurrentUser = Schemas['CurrentUserDto']
export type UserDto = Schemas['UserDto']
export type UserPage = Schemas['UserDtoPagedResult']
export type CreateUserRequest = Schemas['CreateUserCommand']
export type UpdateUserRequest = Schemas['UpdateUserRequest']
export type CreatedResponse = Schemas['CreatedResponse']
