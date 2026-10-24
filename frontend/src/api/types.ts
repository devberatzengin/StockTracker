import type { components } from '@/api/schema'

type Schemas = components['schemas']

export type AccessTokenResponse = Schemas['AccessTokenResponse']
export type CurrentUser = Schemas['CurrentUserDto']
export type UserDto = Schemas['UserDto']
export type UserPage = Schemas['UserDtoPagedResult']
export type CreateUserRequest = Schemas['CreateUserCommand']
export type UpdateUserRequest = Schemas['UpdateUserRequest']
export type CreatedResponse = Schemas['CreatedResponse']

export type TrackingType = Schemas['TrackingType']
export type Unit = Schemas['Unit']
export type BarcodeFormat = Schemas['BarcodeFormat']
export type CategoryDto = Schemas['CategoryDto']
export type CategoryPage = Schemas['CategoryDtoPagedResult']
export type ProductListItem = Schemas['ProductListItemDto']
export type ProductPage = Schemas['ProductListItemDtoPagedResult']
export type ProductDto = Schemas['ProductDto']
export type ProductBarcodeDto = Schemas['ProductBarcodeDto']
export type BarcodeInput = Schemas['BarcodeInput']
export type CreateProductRequest = Schemas['CreateProductCommand']
export type UpdateProductRequest = Schemas['UpdateProductRequest']
