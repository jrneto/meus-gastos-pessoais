# Constitution — GastosApp Frontend

**Escopo: Frontend (React/TypeScript).**

## O que é este sistema
SPA de controle de gastos pessoal, consumindo a API do backend. Não tem
servidor próprio — é um app estático, hoje sem hospedagem definida (ver
seção de infraestrutura).

## Fonte do contrato da API
- **Contrato de wire (endpoints, request/response, status codes):**
  `backend/docs/openapi.json` — gerado a partir do contrato real da API
  (`backend/scripts/export-openapi.sh`), é a fonte primária para
  implementar chamadas HTTP no frontend. Mais compacto e estruturado
  para consulta do que ler `spec.md` em prosa
- **Regras de negócio/validação** (ex.: senha mínima de 8 caracteres,
  formato de campos, quando cada erro ocorre) que o schema OpenAPI não
  expressa: `backend/specs/{FEAT-XX}/spec.md` continua sendo a fonte
- Se o contrato em `backend/docs/openapi.json` parecer desatualizado em
  relação ao comportamento real da API, sinalizar — regenerá-lo é
  responsabilidade do contexto backend, nunca editar esse arquivo à mão

## Regras imutáveis
- Toda feature começa com uma spec em `frontend/specs/{FEAT-XX-nome}/spec.md`
  antes de qualquer código (ver Modo Leve vs Fluxo Completo em `/CLAUDE.md`
  raiz)
- **Nenhuma feature é considerada concluída com testes falhando** — 100%
  dos testes (unitários/componente) devem passar antes de dar uma
  feature por encerrada. Vale tanto para `/review` quanto para
  implementação em Modo Leve
- Token de sessão (access token) vive **apenas em memória** (store
  Zustand), nunca em `localStorage`/`sessionStorage`/cookie legível por
  JS — reduz a superfície de roubo via XSS. Trade-offs documentados em
  `frontend/specs/FEAT-01-setup-login/plan.md`
- Nenhum arquivo `.env*` com valores reais é versionado (`.gitignore`
  cobre `.env*`, exceto `.env.example`) — mesmo quando o conteúdo não
  tem segredo hoje, para manter o hábito consistente e evitar que um
  segredo real seja versionado por engano no futuro
- Toda validação de schema (formulários) usa Zod como fonte única de
  verdade, espelhando as regras já aplicadas pelo backend (ex.: senha
  mínima de 8 caracteres)

## Segurança e custos AWS — nível de preocupação alto
- **Toda infraestrutura é AWS**, seguindo o mesmo princípio adotado no
  backend: **zero custo fixo**. Só recursos serverless / dentro do
  Free Tier (ex.: hosting estático em S3 + CDN via CloudFront). É
  expressamente proibido provisionar qualquer recurso com cobrança por
  hora/instância ligada (ex.: EC2, NAT Gateway, Load Balancer dedicado)
- **Qualquer criação/alteração de recurso AWS que impacte custo ou
  segurança (bucket S3, distribuição CloudFront, domínio, certificado,
  IAM, CORS mais permissivo, exposição pública de algo) exige aprovação
  explícita do usuário antes da implementação** — nunca decidir e criar
  esse tipo de recurso de forma autônoma, mesmo que pareça óbvio ou de
  baixo custo aparente
- Não gerar código Terraform até que seja solicitado explicitamente
  (mesma regra do `/CLAUDE.md` raiz)
- Nenhum segredo (chave de API privada, credencial) deve existir em
  variável `VITE_*` — tudo que o Vite expõe com esse prefixo vai
  embutido, em texto público, no bundle JS entregue ao navegador. Não
  existe "variável de ambiente secreta" no frontend
- Mudanças no contexto backend necessárias para o frontend funcionar
  (ex.: CORS) são tratadas no contexto backend, seguindo as regras dele
  — nunca implementadas "de dentro" do contexto frontend

## Stack
- React + TypeScript, build via Vite
- Roteamento: `react-router-dom`
- Estado global: Zustand
- Formulário: React Hook Form + Zod (`zodResolver`)
- UI: shadcn/ui + Tailwind CSS
- Gráficos: Tremor (adicionado apenas quando a primeira feature que
  precisar de gráfico for planejada — não instalado antecipadamente)
- Testes: Vitest + React Testing Library + MSW (mock de chamadas HTTP)
- HTTP client: `fetch` nativo encapsulado (sem Axios, salvo necessidade
  futura clara)

## Padrão de organização: Feature-based (Bulletproof React)
Clean Architecture em camadas foi avaliada e descartada por adicionar
complexidade desnecessária para o porte do projeto. O padrão adotado é
organização **por feature de negócio** (referência:
[bulletproof-react](https://github.com/alan2207/bulletproof-react)),
hoje o mais usado em produção no ecossistema React:

```
src/
├── app/            # bootstrap: main.tsx, App.tsx, providers, router
├── routes/         # páginas (rotas), compõem componentes das features
├── features/
│   └── {feature}/  # ex.: auth, expenses
│       ├── api/         # chamadas HTTP da feature
│       ├── components/  # componentes React da feature
│       ├── hooks/       # orquestração (casos de uso) da feature
│       ├── schemas/     # Zod
│       ├── store/       # estado global da feature (Zustand)
│       └── errors/      # erros tipados da feature
├── components/
│   ├── ui/          # shadcn/ui — compartilhado entre features
│   └── ...           # componentes compartilhados (ex.: ProtectedRoute)
└── lib/             # utilitários compartilhados (ex.: httpClient)
```

Regra de dependência: `features/*` pode depender de `lib/` e
`components/`; `lib/` e `components/` nunca dependem de `features/*`.
Algo usado por mais de uma feature sobe para `lib/`/`components/` — uma
feature nunca importa de dentro de outra feature.

## Convenções de código
- Erros de integração com API mapeados para classes de erro tipadas
  (ex.: `InvalidCredentialsError`, `NetworkError`) dentro de
  `features/{feature}/errors/`, nunca deixados como exceção genérica
  não tratada nos componentes
- Variáveis de ambiente sempre prefixadas `VITE_`, lidas via
  `import.meta.env`, nunca hardcoded no código
- Chamadas de API ficam em `features/{feature}/api/`, como funções
  simples (sem camada de abstração/interface adicional) — testes mockam
  no nível de rede (MSW), não por injeção de dependência

## Restrições de Arquitetura e Custo (Foco em Free Tier)
- Hosting alvo (a confirmar quando a spec de deploy for criada): S3
  (site estático) + CloudFront — sem servidor rodando, sem contêiner,
  sem função paga por tempo ligado
- Proibido qualquer recurso AWS com cobrança por hora/instância ligada
- Nenhum provisionamento real de infraestrutura acontece sem spec
  própria em `frontend/infra/` e aprovação explícita do usuário
