# Task 12 - Concorrência opcional

## Dependências

Tasks 08 e 09 concluídas e todos os requisitos obrigatórios aprovados.

## Agente recomendado

`dotnet-backend`

## Objetivo

Impedir que duas operações simultâneas consumam o mesmo último item.

## Escopo permitido

- Controle concorrente compatível com PostgreSQL e EF Core.
- Retry técnico somente quando seguro.
- Retorno 409 para a operação perdedora.

## Fora do escopo

- Reserva antecipada ou fila distribuída.

## Testes automatizados

- Produto com saldo 1 e duas baixas simultâneas.
- Uma conclusão, um conflito e saldo final zero.
- Nenhuma quantidade negativa.

## Teste manual

- Opcional; a evidência principal deve ser o teste automatizado concorrente.

## Commit previsto

`feat(inventory): prevent concurrent stock overselling`

