# Tasks — FEAT-14: Migração para o design system Modernist (Login)

- [x] 1. Verificar disponibilidade de `@fontsource-variable/archivo` (pesos 400/600/800) no npm; se não existir nesse formato, definir o fallback (`@fontsource/archivo` ou `@import` escopado) e registrar a escolha
- [x] 2. Adicionar a dependência de fonte escolhida ao `package.json` do app (`frontend/app/package.json`) e instalar
- [x] 3. Criar `frontend/app/src/styles/modernist/modernist.css`: vendorizar os tokens e classes necessárias (`:root` → `.ds-modernist`, reset base, tipografia, `.btn`/`.btn-primary`/`.btn-block`, `.field`/`.input`, `.seg`/`.seg-opt`), com todo seletor global reescrito sob o escopo `.ds-modernist`, e importar a fonte Archivo escolhida na task 1/2
- [x] 4. Criar `frontend/app/src/features/auth/schemas/signupSchema.ts` (campos nome, e-mail, senha — mesmas regras de formato do `loginSchema`) + teste unitário `signupSchema.test.ts`
- [x] 5. Reescrever `frontend/app/src/features/auth/components/LoginForm.tsx`: estado local `authMode` ('login' | 'signup'), segmentado `.seg`/`.seg-opt` para alternar modo, campos `.field`/`.input`, botão `.btn.btn-primary.btn-block`; modo `login` preserva `loginSchema`/`useLogin` sem alteração de comportamento; modo `signup` usa `signupSchema` e navega para `/cadastro-em-breve` no submit sem chamar `authApi`
- [x] 6. Reescrever `frontend/app/src/routes/LoginPage.tsx`: wrapper `.ds-modernist`, wordmark "jrn." (ponto em `--color-accent`) + subtítulo "expenses", mantendo a lógica de redirecionamento reativo (`useAuthSession`) inalterada
- [x] 7. Criar `frontend/app/src/routes/SignupComingSoonPage.tsx`: página estática (wrapper `.ds-modernist`, mensagem de cadastro indisponível, botão/link `.btn-secondary` de volta para `/login`)
- [x] 8. Adicionar a rota pública `/cadastro-em-breve` em `frontend/app/src/app/router.tsx`, como irmã de `/login` (fora de `ProtectedRoute`)
- [x] 9. Atualizar `frontend/docs/constitution.md`: seção "Stack" documentando a UI em transição (Modernist no Login, shadcn/ui + Tailwind no restante) e referenciando `frontend/design-system/` como fonte dos tokens
- [x] 10. Atualizar/ajustar `LoginForm.test.tsx` para o novo markup: garantir que os testes existentes de login (submit válido, credenciais inválidas, validação client-side) continuam passando com os seletores atualizados
- [x] 11. Adicionar teste em `LoginForm.test.tsx` (ou arquivo próprio): alternar para o modo "Criar conta" exibe o campo Nome e troca o rótulo do botão, sem chamar `authApi`/`fetch`
- [x] 12. Adicionar teste em `LoginForm.test.tsx` (ou arquivo próprio): submeter o modo "Criar conta" navega para `/cadastro-em-breve` sem chamar `authApi.login`
- [x] 13. Criar `SignupComingSoonPage.test.tsx`: renderiza o texto de placeholder e o link/botão de volta para `/login`
- [x] 14. Ajustar/criar teste de `LoginPage.tsx` cobrindo a wordmark e a preservação do redirecionamento reativo quando já autenticado
- [x] 15. Rodar a suíte completa (`npm test`) e garantir 100% dos testes passando — 48 arquivos, 209 testes, todos passando
- [x] 16. Revisar manualmente no navegador (`npm run dev`): tela de Login migrada, alternância de modo, submit de login real, submit de "Criar conta" indo para a página fake, e confirmar que nenhuma outra rota do app mudou visualmente — verificado via `tsc -b`, `oxlint` e `npm run build` (sem navegador disponível neste ambiente); revisão visual real pendente do usuário
- [x] 17. Atualizar `spec.md` marcando todos os critérios de aceite concluídos (`- [x]`)
