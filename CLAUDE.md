# Korp - Sistema simplificado de emissão de notas

## Objetivo

Construir o desafio técnico com Angular no frontend e dois microsserviços ASP.NET Core:

- `Inventory.Api`: produtos, saldos e baixas de estoque.
- `Billing.Api`: notas fiscais e fluxo de impressão/fechamento.

O sistema é uma simulação de emissão de notas. Não é uma implementação de NF-e e não integra com SEFAZ.

## Fontes de verdade

- Requisitos: `docs/requirements.md`.
- Arquitetura: `docs/architecture.md`.
- Task atual: arquivo indicado pelo usuário em `docs/tasks/`.
- Progresso: `docs/progress.md`.
- Detalhamento da entrega: `docs/technical-details.md`.

Em caso de divergência, interromper o trabalho, explicar o conflito e pedir decisão. Não resolver divergências silenciosamente.

## Arquitetura obrigatória

- Cada microsserviço é proprietário de seu banco.
- Nenhum serviço acessa diretamente tabelas do outro.
- A comunicação entre serviços ocorre por HTTP.
- O Angular não coordena baixa de estoque e fechamento da nota; chama apenas o Faturamento.
- A nota permanece `Open` até a baixa do Estoque ser confirmada.
- Uma baixa com vários produtos é atômica.
- O `OperationId` protege a baixa contra repetição.
- A numeração da nota é gerada no backend e possui unicidade no banco.
- Erros HTTP usam `ProblemDetails` e não expõem stack trace.

## Organização do código

- Preferir organização por funcionalidade.
- Manter endpoints/controllers pequenos.
- Manter regras de negócio fora da camada HTTP.
- Não criar repositório genérico sobre Entity Framework Core.
- Não expor entidades diretamente pela API.
- Não compartilhar entidades ou `DbContext` entre microsserviços.
- Evitar bibliotecas sem necessidade concreta.
- Não adicionar autenticação, clientes, impostos, SEFAZ, XML fiscal, dashboard ou funcionalidades não solicitadas.

## C# e backend

- Utilizar APIs assíncronas e propagar `CancellationToken`.
- Utilizar LINQ para filtros, projeções, ordenações e validações de coleção.
- Utilizar `AsNoTracking()` em consultas somente leitura quando adequado.
- Validar invariantes também no domínio e no banco.
- Aplicar migrations de forma explícita e documentada.
- Mapear validações para 400, ausências para 404, conflitos de negócio para 409, dependência indisponível para 503 e falhas inesperadas para 500.
- Incluir `traceId` ou correlation ID nos erros e logs.

## Angular

- Utilizar standalone components.
- Utilizar Reactive Forms e `FormArray` para itens da nota.
- Utilizar Signals para estado local simples.
- Utilizar RxJS para HTTP e composição assíncrona.
- Encerrar subscriptions com `takeUntilDestroyed` quando necessário.
- Utilizar `finalize` para indicadores de processamento.
- Não adicionar NgRx sem uma necessidade aprovada.
- Tratar loading, vazio, sucesso e erro nas telas.

## Testes e validação

- Toda funcionalidade exige testes automatizados.
- Testar cenários positivos, validações, conflitos e falhas relevantes.
- Preferir testes de integração com o banco real em container para persistência e concorrência.
- Mocks são aceitáveis para isolar chamadas HTTP em testes específicos, mas não substituem o fluxo integrado obrigatório.
- Backend aprovado somente após build, testes e roteiro manual no Swagger.
- Frontend aprovado somente após formatação, lint, testes, build e validação no navegador.

## Fluxo de trabalho

1. Executar `git status` antes de qualquer alteração.
2. Ler este arquivo, a arquitetura e a task indicada.
3. Implementar somente a task atual.
4. Não antecipar tasks futuras.
5. Preservar alterações preexistentes não relacionadas.
6. Executar todas as validações da task.
7. Apresentar arquivos alterados, decisões, testes e roteiro manual.
8. Parar sem criar commit.
9. Criar commit somente após aprovação explícita do usuário via `/create-checkpoint`.

## Git

- Commits seguem Conventional Commits.
- Código e seus testes ficam no mesmo commit.
- Não usar `git add .` sem antes revisar todos os arquivos.
- Não reescrever histórico.
- Não executar push automaticamente.
- Não incluir `.env`, credenciais, certificados, dados pessoais ou arquivos gerados desnecessários.
- Não misturar mudanças de tasks diferentes no mesmo commit.

## Relatório obrigatório ao final de cada task

- Estado inicial e final do Git.
- Arquivos criados e alterados.
- Requisitos atendidos.
- Decisões e premissas.
- Comandos executados.
- Resultado dos testes.
- Roteiro de validação manual.
- Pendências e limitações.
- Confirmação de que nenhum commit foi criado.

