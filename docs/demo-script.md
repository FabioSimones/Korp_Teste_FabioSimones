# Roteiro de demonstração (10–15 minutos)

Roteiro para gravar o vídeo de entrega ou apresentar o sistema ao vivo. Cada passo indica a
ferramenta a usar, a ação, o resultado esperado, a evidência a mostrar em tela e — quando aplicável
— o plano de recuperação caso os dados já existam de uma execução anterior.

Use sempre um **código de produto único por execução** (ex.: sufixo com timestamp,
`DEMO-<hhmmss>`) para nunca colidir com produtos cadastrados em demonstrações anteriores.

Pré-condição: containers do `docker compose` `healthy`, migrations aplicadas, `Inventory.Api`
(`:5081`), `Billing.Api` (`:5082`) e Angular (`:4200`) em execução — ver README, seções 9 a 14.

## 1. Introdução (≈ 1 min)

- **Ferramenta**: fala/tela cheia (nenhuma ação no sistema).
- **Ação**: apresentar o objetivo do desafio (simulação de emissão de notas, não é NF-e real),
  a stack (Angular + dois microsserviços ASP.NET Core, cada um com seu PostgreSQL) e o diagrama de
  arquitetura do README (seção 4).
- **Resultado esperado**: audiência entende que Estoque e Faturamento são serviços independentes,
  com bancos próprios, comunicando-se por HTTP.
- **Evidência a mostrar**: diagrama em texto do README/`docs/architecture.md`.
- **Recuperação**: não aplicável (passo apenas expositivo).

## 2. Containers e infraestrutura (≈ 1 min)

- **Ferramenta**: terminal.
- **Ação**: `docker compose ps`.
- **Resultado esperado**: `inventory-db` e `billing-db` com status `healthy`.
- **Evidência a mostrar**: saída do comando na tela.
- **Recuperação**: se algum container não estiver `healthy`, rodar `docker compose up -d` e
  aguardar; **não** usar `docker compose down -v`.

## 3. Cadastrar produto (≈ 1 min)

- **Ferramenta**: navegador (Angular, tela de Produtos).
- **Ação**: cadastrar um produto com código único da demo (ex.: `DEMO-143205`), descrição e saldo
  inicial (ex.: `5`).
- **Resultado esperado**: produto aparece na listagem com o saldo cadastrado.
- **Evidência a mostrar**: formulário preenchido, confirmação de sucesso, produto na listagem.
- **Recuperação**: se o código já existir (execução repetida sem trocar o sufixo), gerar um novo
  sufixo e recadastrar — não reaproveitar o produto de uma demo anterior sem necessidade.

## 4. Tentar código duplicado (≈ 1 min)

- **Ferramenta**: navegador (Angular, tela de Produtos) ou Swagger de `Inventory.Api`.
- **Ação**: tentar cadastrar novamente o mesmo código usado no passo 3.
- **Resultado esperado**: erro de validação amigável, HTTP `409` (código duplicado), produto não é
  duplicado na listagem.
- **Evidência a mostrar**: mensagem de erro na tela (ou resposta `409` + `ProblemDetails` no
  Swagger).
- **Recuperação**: não aplicável — é o próprio cenário de falha esperado.

## 5. Criar nota (≈ 1–2 min)

- **Ferramenta**: navegador (Angular, tela de Notas).
- **Ação**: criar uma nova nota incluindo o produto cadastrado no passo 3 (quantidade menor que o
  saldo, ex.: `2` de `5`), podendo adicionar mais de um item se houver outro produto disponível.
- **Resultado esperado**: nota criada com numeração sequencial gerada pelo backend e status
  `Open`.
- **Evidência a mostrar**: número da nota e status `Open` na confirmação/detalhe.
- **Recuperação**: se a criação falhar por saldo insuficiente (produto residual de outra demo com
  saldo zerado), usar o produto criado no passo 3 desta mesma execução.

## 6. Listar e abrir detalhes (≈ 1 min)

- **Ferramenta**: navegador (Angular, tela de Notas).
- **Ação**: voltar à listagem de notas, localizar a nota criada e abrir os detalhes.
- **Resultado esperado**: detalhe mostra número, status `Open`, itens e quantidades corretos.
- **Evidência a mostrar**: tela de detalhe da nota.
- **Recuperação**: usar a busca/ordenação da listagem para localizar a nota pelo número mostrado
  no passo 5.

## 7. Imprimir (≈ 1 min)

- **Ferramenta**: navegador (Angular, tela de detalhe da nota).
- **Ação**: clicar em "Imprimir".
- **Resultado esperado**: indicador de processamento aparece durante a chamada; ao concluir, a
  nota muda para `Closed` e a visualização de impressão é aberta.
- **Evidência a mostrar**: spinner durante o processamento, badge de status mudando para
  `Closed`, data de fechamento preenchida.
- **Recuperação**: não aplicável — fluxo feliz.

## 8. Mostrar nota fechada (≈ 30 s)

- **Ferramenta**: navegador (Angular, tela de detalhe da nota).
- **Ação**: apontar o badge `Closed`, a data de fechamento e o botão "Imprimir" desabilitado com a
  dica "Esta nota já está fechada.".
- **Resultado esperado**: não é possível imprimir novamente pela UI.
- **Evidência a mostrar**: botão desabilitado + tooltip/texto de ajuda.
- **Recuperação**: não aplicável.

## 9. Mostrar saldo reduzido (≈ 1 min)

- **Ferramenta**: navegador (Angular, tela de Produtos) ou Swagger de `Inventory.Api`
  (`GET /api/products`).
- **Ação**: consultar o produto usado na nota e comparar o saldo atual com o saldo antes da
  impressão (passo 3: `5` → após debitar `2`: `3`).
- **Resultado esperado**: saldo debitado exatamente na quantidade da nota, uma única vez.
- **Evidência a mostrar**: saldo atualizado na listagem/Swagger.
- **Recuperação**: não aplicável — apenas leitura.

## 10. Repetir impressão e confirmar 409 (≈ 1 min)

- **Ferramenta**: Swagger de `Billing.Api` (`POST /api/invoices/{id}/print`) ou nova tentativa pela
  UI, se acessível.
- **Ação**: chamar `POST /print` novamente para a mesma nota, já `Closed`.
- **Resultado esperado**: HTTP `409` com `ProblemDetails` (nota já fechada); saldo do produto
  **não** muda.
- **Evidência a mostrar**: resposta `409` no Swagger; saldo do passo 9 inalterado.
- **Recuperação**: não aplicável — é o próprio cenário de validação.

## 11. Explicar idempotência (≈ 1 min)

- **Ferramenta**: fala/tela cheia, apoiado no Swagger de `Inventory.Api`
  (`POST /api/stock/debits`) se quiser demonstrar tecnicamente.
- **Ação**: explicar que o `OperationId` gerado pelo `Billing.Api` é persistido antes da chamada ao
  Estoque, e que uma repetição do mesmo `OperationId` devolve o resultado já registrado sem debitar
  o saldo de novo — é isso que sustenta o `409` do passo 10 e a recuperação segura após falha de
  rede.
- **Resultado esperado**: audiência entende por que reimprimir não duplica a baixa.
- **Evidência a mostrar**: nenhuma ação nova; reforça os resultados dos passos 7–10.
- **Recuperação**: não aplicável.

## 12. Demonstrar falha do Inventory e 503 (≈ 2 min)

- **Ferramenta**: terminal (parar `Inventory.Api`) + navegador ou Swagger (`Billing.Api`).
- **Ação**: encerrar o processo do `Inventory.Api` (`Ctrl+C` no terminal correspondente); criar uma
  nova nota (se ainda não existir uma `Open` disponível) e tentar imprimir.
- **Resultado esperado**: após esgotar as tentativas de retry/circuit breaker, a chamada falha com
  `503` + `ProblemDetails` (com `traceId`); a nota permanece `Open`.
- **Evidência a mostrar**: mensagem de erro amigável na UI ("Serviço de estoque indisponível...")
  ou resposta `503` no Swagger; nota continua `Open`.
- **Recuperação**: reiniciar `Inventory.Api` (`dotnet run` no diretório do projeto) e repetir a
  impressão da mesma nota para mostrar que ela conclui normalmente, sem baixa duplicada.

## 13. Explicar concorrência da última unidade (≈ 1–2 min)

- **Ferramenta**: fala/tela cheia, apoiado em `docs/technical-details.md` (seção de concorrência)
  ou nos testes automatizados de `StockConcurrencyApiTests.cs`.
- **Ação**: explicar o cenário de duas baixas concorrentes disputando a última unidade de um
  produto, e como o `SELECT ... FOR UPDATE` (bloqueio de linha, ordem determinística por `Id`)
  impede que ambas debitem a mesma unidade — apenas uma recebe sucesso, a outra recebe `409` por
  saldo insuficiente (ou o resultado idempotente, se for o mesmo `OperationId`).
- **Resultado esperado**: audiência entende que a proteção é do banco (lock de linha), não da
  aplicação em memória.
- **Evidência a mostrar**: trecho relevante de `docs/technical-details.md` ou nome dos testes de
  concorrência passando (ver passo 14).
- **Recuperação**: não aplicável.

## 14. Mostrar testes (≈ 1–2 min)

- **Ferramenta**: terminal.
- **Ação**: `dotnet test src/backend/Korp.sln --configuration Release` (backend) e `npm test`
  (frontend, em `src/frontend/invoice-web`).
- **Resultado esperado**: 116 testes de backend (55 Inventory + 61 Billing) e 58 testes de
  frontend, todos aprovados.
- **Evidência a mostrar**: resumo final de cada execução (`Aprovado! ...` / `Tests ... passed`).
- **Recuperação**: se algum teste falhar por Docker não estar em execução, iniciar o Docker Desktop
  e repetir.

## 15. Encerramento (≈ 1 min)

- **Ferramenta**: fala/tela cheia, apoiado no README.
- **Ação**: mostrar o README (comandos de execução do zero) e resumir as decisões e limitações
  conhecidas: impressão concorrente da mesma nota não tem proteção dedicada; circuit breaker é em
  memória por processo; contenção no mesmo produto é serializada por lock de linha (esperado);
  não há autenticação (fora do escopo do desafio).
- **Resultado esperado**: audiência sai com uma visão honesta do que foi entregue e do que é
  limitação conhecida, sem prometer recursos inexistentes.
- **Evidência a mostrar**: seção "Limitações conhecidas" do README.
- **Recuperação**: não aplicável.
