# Architecture — GastosApp (backend)

A visão de arquitetura do sistema como um todo (frontend + backend, C4
model até o nível 3) foi movida para **[`/docs/architecture.md`](../../docs/architecture.md)**,
na raiz do monorepo — nenhum dos dois contextos, isoladamente,
representa a arquitetura completa.

Este arquivo continua existindo neste caminho só para não quebrar
referências (`backend/CLAUDE.md`, `/plan`), mas não tem mais conteúdo
próprio. Para os detalhes específicos do backend:

- **Modelo de dados / tabela DynamoDB** (item types, PK/SK, GSIs,
  access patterns): [`data-model.md`](./data-model.md)
- **Infraestrutura de produção/homologação/local** (Lambda, API
  Gateway, Terraform, ambiente Docker local):
  [`../infra/CLAUDE.md`](../infra/CLAUDE.md)
- **Stack e estrutura de projetos (Clean Architecture)**:
  [`../CLAUDE.md`](../CLAUDE.md)
