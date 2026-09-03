export {
  SessionExpiredError,
  NetworkError,
  UnknownCategoryError,
} from '@/lib/categories/categoryErrors'

export class ValidationError extends Error {
  constructor() {
    super('Não foi possível salvar a categoria. Verifique os dados informados.')
    this.name = 'ValidationError'
  }
}

export class NameConflictError extends Error {
  constructor() {
    super('Já existe uma categoria com esse nome.')
    this.name = 'NameConflictError'
  }
}

export class ForbiddenError extends Error {
  constructor() {
    super('Seu nível de acesso não permite esta ação.')
    this.name = 'ForbiddenError'
  }
}

export class CategoryInUseError extends Error {
  constructor() {
    super('Esta categoria não pode ser excluída enquanto houver despesas associadas a ela.')
    this.name = 'CategoryInUseError'
  }
}

export class NotFoundError extends Error {
  constructor() {
    super('Categoria não encontrada.')
    this.name = 'NotFoundError'
  }
}
