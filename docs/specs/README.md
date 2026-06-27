# Spec-Driven Development (SDD) — GastosApp

Este projeto utiliza o processo de **Desenvolvimento Orientado por Especificação (SDD)**.

## Fluxo de Desenvolvimento

1. **Escrever a Spec**: Antes de começar a desenvolver qualquer nova funcionalidade ou realizar alterações significativas, deve ser criada uma especificação sob a pasta `/docs/specs/` com a extensão `.md` (ex: `docs/specs/001-add-transaction.md`).
2. **Revisar e Aprovar**: A especificação é validada em conjunto pelo desenvolvedor e o assistente de IA.
3. **Implementar**: O código é desenvolvido seguindo estritamente o que foi acordado na especificação.
4. **Testar**: Os testes unitários e de integração validam as regras descritas na especificação.

## Estrutura de uma Spec

Uma especificação típica deve conter:
- **Título e Contexto**: O que é a funcionalidade e o valor gerado.
- **Requisitos de Negócio**: Regras de validação, limites, tratamentos de erro.
- **Definição técnica**:
  - Endpoints (caminho, verbo HTTP, cabeçalhos, corpo do request/response).
  - Padrão do DynamoDB (PK, SK, GSI1PK, GSI1SK de entrada e saída).
  - Caso de uso na camada Application.
- **Plano de Testes**: Quais cenários serão testados (sucesso, erros de validação, erros de infra).
