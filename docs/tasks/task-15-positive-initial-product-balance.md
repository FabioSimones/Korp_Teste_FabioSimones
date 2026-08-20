# Task 15 — Exigir saldo inicial positivo no cadastro de produtos

## Objetivo

Impedir o cadastro de um novo produto com saldo inicial igual a zero ou negativo,
sem afetar a capacidade de uma baixa de estoque levar o saldo de um produto já
existente exatamente a zero.

## Regra de negócio

> Todo novo produto deve ser cadastrado com saldo inicial inteiro maior que zero.

- Saldo inicial `1` ou maior: permitido.
- Saldo inicial `0`: rejeitado.
- Saldo inicial negativo: rejeitado.
- Saldo decimal: rejeitado (o campo é inteiro em todas as camadas).
- Produtos antigos já cadastrados com saldo `0` continuam existindo normalmente;
  nenhuma informação existente é apagada ou ajustada.

## Exemplos

| Entrada (criação) | Resultado |
| --- | --- |
| `code=SKU-1, description=Widget, balance=1` | `201 Created` |
| `code=SKU-2, description=Widget, balance=10` | `201 Created` |
| `code=SKU-3, description=Widget, balance=0` | `400 Bad Request` (`INVALID_PRODUCT`) |
| `code=SKU-4, description=Widget, balance=-1` | `400 Bad Request` (`INVALID_PRODUCT`) |
| `code=SKU-5, description=Widget, balance=1.5` | rejeitado no model binding / frontend (campo é inteiro) |

## Separação entre saldo inicial e saldo atual

A regra se aplica **somente** ao momento de criação do produto (`Product.Create`).
O saldo **atual** de um produto já existente continua podendo chegar a zero através
de uma baixa de estoque (`Product.Debit`), que já garante que o saldo nunca fica
negativo.

Cenário de regressão obrigatório:

1. Produto criado com saldo `1`.
2. Baixa de quantidade `1`.
3. Saldo final `0` — válido.

## Escopo

- `Inventory.Api`: entidade `Product` (regra de criação), testes de domínio e de API.
- Frontend Angular: modal de cadastro de produto (`product-form-dialog`).
- Documentação (`docs/progress.md`, `docs/technical-details.md`, este arquivo).

## Fora de escopo

- `Billing.Api` (não alterado).
- Paginação, ordenação, CORS, resiliência, idempotência ou concorrência (não alterados).
- Endpoints existentes (assinatura/rota/contrato inalterados; apenas a regra de
  validação de `POST /api/products` fica mais restritiva).
- Migration de banco: a constraint `CK_products_balance_non_negative`
  (`"Balance" >= 0`) já representa corretamente a regra de saldo **atual** e
  permanece inalterada. A regra de saldo **inicial** (`> 0`) é exclusiva do
  momento de criação e não é representável como constraint de coluna sem
  impedir baixas legítimas até zero — por isso fica somente na camada de
  domínio/serviço.
- Ajuste ou remoção de produtos já cadastrados com saldo zero.

## Arquivos previstos

- `src/backend/Inventory.Api/Features/Products/Product.cs`
- `tests/Inventory.Tests/ProductDomainTests.cs`
- `tests/Inventory.Tests/ProductsApiTests.cs`
- `src/frontend/invoice-web/src/app/features/products/product-form-dialog/product-form-dialog.ts`
- `src/frontend/invoice-web/src/app/features/products/product-form-dialog/product-form-dialog.html`
- `src/frontend/invoice-web/src/app/features/products/product-form-dialog/product-form-dialog.spec.ts`
- `docs/progress.md`
- `docs/technical-details.md`
- `docs/tasks/task-15-positive-initial-product-balance.md` (este arquivo)

## Critérios de aceite

- `POST /api/products` com `balance <= 0` retorna `400 Bad Request`, com
  `errorCode=INVALID_PRODUCT`, `traceId` e mensagem em português, sem persistir
  nenhum registro.
- `POST /api/products` com `balance >= 1` continua retornando `201 Created` e
  persistindo o produto normalmente.
- Uma baixa de estoque que leve um produto de saldo `1` a saldo `0` continua
  funcionando (nenhuma regressão em `Product.Debit`).
- A constraint de banco `CK_products_balance_non_negative` permanece inalterada
  e nenhuma migration é criada.
- Produtos pré-existentes com saldo `0` não são alterados nem removidos.
- No frontend, o campo de saldo inicial do modal de cadastro:
  - começa vazio (não pré-preenchido com `0`);
  - aceita apenas inteiros maiores que zero;
  - exibe mensagem inline em português para `0`, negativos e decimais;
  - mantém o botão "Cadastrar produto" desabilitado enquanto o formulário for inválido;
  - dispara no máximo um toast de erro por resposta de erro do backend.

## Roteiro de validação manual

1. Abrir `/produtos`.
2. Clicar em "Novo produto".
3. Confirmar que o campo saldo começa vazio.
4. Informar código e descrição válidos.
5. Digitar `0`: mensagem inline em português; botão desabilitado; nenhuma requisição `POST`.
6. Digitar `-1`: mensagem inline; botão desabilitado; nenhuma requisição `POST`.
7. Tentar digitar `1.5`: valor rejeitado ou campo inválido; nenhuma requisição `POST`.
8. Digitar `1`: formulário válido; cadastro realizado; toast verde único; modal
   fechado; produto presente na listagem.
9. Pelo Swagger, enviar saldo `0` e `-1` em `POST /api/products`: `400`, mensagem
   em português, `errorCode=INVALID_PRODUCT`, `traceId` presente.
10. Criar produto com saldo `1`, incluí-lo em uma nota e imprimir quantidade `1`:
    impressão concluída, saldo final do produto igual a `0`, nenhuma violação de
    constraint.

## Testes necessários

### Domínio (`ProductDomainTests`)

- Criar produto com saldo `1` funciona.
- Criar produto com saldo `0` lança `ProductValidationException`.
- Criar produto com saldo negativo lança `ProductValidationException`.
- Produto com saldo `1` sofre baixa de `1` e o saldo final é `0`.
- Baixa que deixaria saldo negativo continua rejeitada (já coberto por teste existente).

### API com PostgreSQL real (`ProductsApiTests`)

- `POST /api/products` com saldo `0` retorna `400`.
- `POST /api/products` com saldo negativo retorna `400`.
- A resposta contém mensagem em português, `errorCode=INVALID_PRODUCT` e `traceId`.
- Nenhum produto é persistido nesses casos.
- Saldo `1` retorna `201` e persiste corretamente.
- Endpoints antigos e paginados continuam funcionando (suíte existente, sem alteração de contrato).

### Frontend (`product-form-dialog.spec.ts`)

- Campo de saldo inicia vazio.
- Saldo `0` deixa o formulário inválido, exibe mensagem inline e mantém o botão desabilitado.
- Saldo negativo é rejeitado.
- Saldo decimal é rejeitado.
- Saldo `1` habilita o botão quando os demais campos são válidos.
- Nenhum `POST` é disparado com formulário inválido.
- Erro `400` do backend produz exatamente um toast (comportamento já coberto pelos testes existentes de erro).
- Cadastro válido fecha o modal e atualiza a listagem (comportamento já coberto pelos testes existentes de sucesso).
