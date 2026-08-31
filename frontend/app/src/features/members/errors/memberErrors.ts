export class SessionExpiredError extends Error {
  constructor() {
    super('Sua sessão expirou. Faça login novamente.')
    this.name = 'SessionExpiredError'
  }
}

export class NetworkError extends Error {
  constructor() {
    super('Não foi possível conectar à API. Verifique sua conexão.')
    this.name = 'NetworkError'
  }
}

export class ValidationError extends Error {
  constructor() {
    super('Preencha um e-mail e um nível de acesso válidos.')
    this.name = 'ValidationError'
  }
}

export class ForbiddenError extends Error {
  constructor() {
    super('Seu nível de acesso não permite esta ação.')
    this.name = 'ForbiddenError'
  }
}

export class NotFoundError extends Error {
  constructor() {
    super('Membro não encontrado.')
    this.name = 'NotFoundError'
  }
}

export class ConflictError extends Error {
  constructor() {
    super('Este e-mail já é membro desta conta.')
    this.name = 'ConflictError'
  }
}

export class CannotModifyTitularError extends Error {
  constructor() {
    super('O papel do Titular não pode ser alterado.')
    this.name = 'CannotModifyTitularError'
  }
}

export class CannotRemoveTitularError extends Error {
  constructor() {
    super('O Titular da conta não pode ser removido.')
    this.name = 'CannotRemoveTitularError'
  }
}

export class UnknownMemberError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado. Tente novamente.')
    this.name = 'UnknownMemberError'
  }
}
