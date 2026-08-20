# Task 16 — Documentação visual e execução assistida

> Renumerada de "Task 15" para "Task 16" durante o diagnóstico desta task: a Task 15
> (`Exigir saldo inicial positivo no cadastro de produtos`) já existia em `docs/progress.md`,
> concluída no commit `d9f64b8`. Decisão confirmada com o usuário antes de qualquer alteração.

## Objetivo

1. Adicionar ao projeto imagens reais do sistema em funcionamento.
2. Criar uma apresentação visual clara para quem acessar o repositório.
3. Auditar e melhorar o passo a passo de execução local.
4. Garantir que uma pessoa que acabou de clonar o projeto consiga configurar, executar, testar e
   encerrar o ambiente sem depender de conhecimento prévio da implementação.

Esta é uma task **exclusivamente documental** — nenhum código de aplicação, teste, migration ou
configuração funcional é alterado.

## Escopo

- `README.md`: seção "Visão do sistema" (galeria de até 6 imagens principais), reorganização em
  "Início rápido" + "Execução detalhada", seção de URLs com health checks, seção "Troubleshooting"
  consolidada, contagens de teste atualizadas.
- `docs/screenshots.md` (novo, se necessário): galeria completa, caso mais de 6 imagens sejam úteis.
- `docs/images/screenshots/`: estrutura de pastas e convenção de nomes para as capturas de tela.
- `docs/demo-script.md`: apenas a correção das contagens de teste desatualizadas (116/58 →
  contagem real), sem alterar o roteiro em si.
- `docs/progress.md`: entrada da Task 16 como `Em validação`.
- Este arquivo (`docs/tasks/task-16-visual-documentation-and-setup-guide.md`).

## Fora de escopo

- Qualquer alteração em código C#, Angular, testes automatizados, migrations, `docker-compose.yml`
  ou configurações funcionais (contratos HTTP, portas, CORS, etc.).
- Instalação de dependências ou ferramentas novas.
- Correção do conteúdo de `COMO-USAR.md` — identificado como desatualizado durante o diagnóstico
  (descreve skills/agentes que não existem mais no projeto), mas está fora do escopo desta task
  (é sobre o workflow do Claude Code, não sobre o setup do produto). Reportado como achado, não
  corrigido aqui.
- Gravação de arquivos de imagem binários pelo assistente: as capturas de tela anexadas ao pedido
  não puderam ser extraídas e salvas diretamente no repositório (limitação de ferramentas
  disponíveis nesta sessão); o usuário precisa copiá-las manualmente para os caminhos indicados no
  relatório final.
- Marcar a Task 16 como `Concluída`, criar commit ou executar push.

## Regra de não alteração do código da aplicação

Nenhum arquivo em `src/backend/**/*.cs` (fora de `docs/`), `src/frontend/**/*.ts`, `*.html`,
`*.scss` de componentes, `tests/**`, `**/Data/Migrations/**` ou `docker-compose.yml` é tocado por
esta task. Qualquer divergência técnica real encontrada durante o diagnóstico é reportada, nunca
corrigida "no código" como parte desta entrega documental.

## Arquivos previstos

- `README.md` (alterado)
- `docs/demo-script.md` (alterado — apenas contagens de teste)
- `docs/progress.md` (alterado)
- `docs/screenshots.md` (novo, galeria completa)
- `docs/images/screenshots/README.md` (novo — convenção de nomes e instruções de cópia manual)
- `docs/tasks/task-16-visual-documentation-and-setup-guide.md` (este arquivo)

## Critérios de aceite

- README possui uma apresentação visual coerente, com legendas ligadas a requisitos funcionais
  reais.
- Toda imagem referenciada tem texto alternativo e legenda.
- Nenhuma imagem com dado sensível é referenciada.
- Todos os links relativos apontam para caminhos reais dentro do repositório.
- O "Início rápido" permite executar o projeto sem consultar múltiplos documentos.
- O guia detalhado cobre configuração, migrations, execução, testes e encerramento.
- `.env` e User Secrets continuam com responsabilidades claramente separadas.
- O conflito da porta 5432 permanece documentado com alternativas seguras (sem remoção de volumes
  como primeira opção).
- Testcontainers é explicado na seção de testes/troubleshooting.
- Nenhum código de aplicação foi alterado.
- Nenhum segredo real foi versionado.
- Task 16 permanece `Em validação` ao final.
- Nenhum commit ou push é realizado.

## Roteiro de validação

1. Renderizar `README.md` e conferir se a seção "Visão do sistema" aparece entre a arquitetura e as
   instruções de instalação.
2. Conferir que cada imagem referenciada existe fisicamente em `docs/images/screenshots/` (após a
   cópia manual) e que o caminho relativo resolve corretamente a partir da raiz do repositório.
3. Rodar `docker compose config --quiet` (validação de sintaxe, sem subir containers).
4. Conferir manualmente cada comando documentado contra os arquivos reais (`.csproj`,
   `package.json`, `launchSettings.json`, `environment.development.ts`).
5. Conferir que as portas e URLs citadas (`5081`, `5082`, `4200`, `5433`, `5434`, `/health`,
   `/swagger`) batem com o código.
6. Conferir ausência de caminhos absolutos do Windows/usuário em qualquer link ou referência de
   imagem.
7. Revisão visual de cada captura de tela quanto a dados sensíveis antes de instruir sua cópia.

## Limitações

- A validação de "clonar, configurar e subir do zero" não foi executada de ponta a ponta nesta
  sessão (os bancos/containers já existiam de sessões anteriores) — o roteiro foi validado por
  **inspeção** do código/configuração real, não por execução limpa completa. Detalhado no relatório
  final desta task.
- Não há captura de tela da visualização preparada para impressão (`invoice-print-view`) entre as
  imagens fornecidas — a galeria principal fica com 5 das 6 posições preenchidas até que essa
  captura seja fornecida.
