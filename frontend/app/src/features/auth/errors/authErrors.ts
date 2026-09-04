export class InvalidCredentialsError extends Error {
  constructor() {
    super('Email ou senha inválidos.')
    this.name = 'InvalidCredentialsError'
  }
}

export class NetworkError extends Error {
  constructor() {
    super('Não foi possível conectar à API. Verifique sua conexão.')
    this.name = 'NetworkError'
  }
}

export class UnknownAuthError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado. Tente novamente.')
    this.name = 'UnknownAuthError'
  }
}

export class RefreshFailedError extends Error {
  constructor() {
    super('Refresh token ausente, inválido ou expirado.')
    this.name = 'RefreshFailedError'
  }
}

export class EmailAlreadyExistsError extends Error {
  constructor() {
    super('Este email já está cadastrado.')
    this.name = 'EmailAlreadyExistsError'
  }
}

export class CpfAlreadyExistsError extends Error {
  constructor() {
    super('Este CPF já está cadastrado.')
    this.name = 'CpfAlreadyExistsError'
  }
}

export class RegisterValidationError extends Error {
  constructor() {
    super('Não foi possível concluir o cadastro. Verifique os dados informados.')
    this.name = 'RegisterValidationError'
  }
}

// Renomeado de `AccountPendingApprovalError` (FEAT-21) na FEAT-31: não
// existe mais aprovação manual de administrador — a confirmação é via
// código OTP enviado por email (`ConfirmationForm`).
export class AccountNotConfirmedError extends Error {
  constructor() {
    super('Confirme seu cadastro pelo código enviado por e-mail antes de entrar.')
    this.name = 'AccountNotConfirmedError'
  }
}

export class InvalidConfirmationCodeError extends Error {
  constructor() {
    super('Código inválido ou expirado. Confira o email ou solicite um novo código.')
    this.name = 'InvalidConfirmationCodeError'
  }
}
