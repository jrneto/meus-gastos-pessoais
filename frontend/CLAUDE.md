# Frontend GastosApp — Contexto para IA

**Antes de gerar código neste diretório, leia sempre:**
`frontend/docs/constitution.md` (regras imutáveis, incluindo segurança
e custo AWS). Ao trabalhar dentro de `frontend/infra/`, leia também
`frontend/infra/CLAUDE.md`. Para o critério de quando abrir uma spec,
veja "Modo Leve vs Fluxo Completo" no [`/CLAUDE.md`](../CLAUDE.md) raiz.

## Stack
- React + TypeScript, build via Vite
- Roteamento: `react-router-dom`
- Estado global: Zustand
- Formulário: React Hook Form + Zod (`zodResolver`)
- UI: design system **Modernist** (`.ds-modernist`, migração tela por
  tela desde a FEAT-14) sobre a base shadcn/ui + Tailwind CSS ainda em
  uso nas telas não migradas — ver estado atual da migração em
  `docs/constitution.md`
- Gráficos: Tremor (só quando a primeira feature de gráfico existir)
- Testes: Vitest + React Testing Library + MSW
- Consome a API do backend (`backend/specs/`) via HTTP — nenhuma lógica
  de negócio duplicada no frontend

## Estrutura de projeto (Feature-based / Bulletproof React)

O projeto Vite fica isolado em `frontend/app/`, separado de
`docs/`/`specs/`/`infra/` — mesmo princípio do backend
(`backend/src/` guarda o código, `docs/`/`specs/`/`infra/` ficam ao
lado, fora da árvore de código):

```
frontend/
├── CLAUDE.md
├── README.md
├── docs/
│   ├── constitution.md
│   ├── backlog.md                 # débitos técnicos e melhorias futuras
│   └── README.md                  # explica o fluxo SDD do frontend
├── specs/
│   └── FEAT-XX-nome-feature/{spec.md, plan.md, tasks.md}
├── infra/
│   └── CLAUDE.md                  # contexto de infra do frontend (S3+CloudFront+WAF+DNS, prod/hom, Terraform)
└── app/                            # projeto Vite (package.json aqui)
    ├── src/
    │   ├── app/                    # bootstrap: main.tsx, App.tsx, providers, router
    │   ├── routes/                 # páginas (rotas), compõem componentes das features
    │   ├── features/
    │   │   └── {feature}/          # ex.: auth, expenses (uma pasta por domínio de negócio)
    │   │       ├── api/                # chamadas HTTP da feature
    │   │       ├── components/         # componentes React da feature
    │   │       ├── hooks/               # orquestração (casos de uso) da feature
    │   │       ├── schemas/             # Zod
    │   │       ├── store/               # estado global da feature (Zustand)
    │   │       └── errors/              # erros tipados da feature
    │   ├── components/
    │   │   ├── ui/                 # shadcn/ui (gerado via CLI)
    │   │   └── ...                  # componentes compartilhados entre features
    │   └── lib/                    # utilitários compartilhados (ex.: httpClient)
    ├── .env.example
    └── vite.config.ts
```

Regra de dependência: `features/*` pode depender de `lib/` e
`components/`; `lib/`/`components/` nunca dependem de `features/*`; uma
feature nunca importa de dentro de outra.

## Convenções que valem só para o frontend
- Toda feature nova com layout/UI segue o design system **Modernist**
  em `frontend/design-system/` como referência — leia
  `frontend/design-system/README.md` antes de desenhar/implementar (ver
  `docs/constitution.md`)
- Token de sessão em memória (Zustand), nunca em `localStorage`/
  `sessionStorage` (ver `docs/constitution.md`)
- Nenhum `.env*` com valor real é versionado — só `.env.example`
- `VITE_API_BASE_URL` configura o ambiente (local vs produção AWS), sem
  necessidade de alterar código
- Chamadas de API em `features/{feature}/api/`, sem camada de interface/
  abstração adicional — mocks de teste feitos via MSW, no nível de rede
- Erros de API mapeados para classes tipadas em
  `features/{feature}/errors/`
- Qualquer recurso AWS que impacte custo ou segurança exige aprovação
  explícita do usuário antes de criar (ver `docs/constitution.md`)
- **Toda feature só é considerada concluída com 100% dos testes
  passando**

## Comandos úteis

```bash
cd frontend/app
npm install
npm run dev            # sobe local apontando para VITE_API_BASE_URL de .env.development
npm run build           # build de produção, usa .env.production
npm test                 # Vitest
```

## Padrão de spec (Fluxo Completo)

Cada feature vive em `frontend/specs/{FEAT-XX-nome-feature}/`, nunca
como arquivo solto. Use os comandos `/specify`, `/plan`, `/tasks` e
`/review` para o ciclo completo, mesma lógica adotada no backend.
