using Xunit;

// Achado real durante a implementação da FEAT-32 (primeira vez que a
// suíte tem mais de uma classe de teste): por padrão o xUnit roda
// classes de teste diferentes em paralelo (só serializa métodos dentro
// da mesma classe) — contra o modo local (Category=Integration via
// LambdaRieTransport), isso derruba a conexão com o container do
// Runtime Interface Emulator, que emula o modelo de execução do Lambda
// real (uma invocação de cada vez, sem suportar concorrência).
// Desabilitar o paralelismo do assembly inteiro resolve pra este e
// qualquer módulo futuro — consistente com o alvo real da suíte (API
// real compartilhada, hom/prod incluídos), não desenhado pra receber
// requisições concorrentes de uma mesma execução de teste. Ver
// backend/specs/FEAT-32-testes-integrados-modulos-pendentes/plan.md.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
