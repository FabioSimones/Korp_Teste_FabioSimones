# Task 10 - Impressão no frontend

## Dependências

Tasks 07 e 09 concluídas.

## Agente recomendado

`angular-frontend`

## Objetivo

Implementar o fluxo visual de impressão e fechamento.

## Escopo permitido

- Botão `Imprimir` visível nos detalhes.
- Bloqueio para nota fechada.
- Spinner durante a chamada.
- Atualização do status após sucesso.
- Mensagens de saldo insuficiente e indisponibilidade.
- Componente/rota imprimível.
- CSS `@media print` e `window.print()` após sucesso.

## Fora do escopo

- Geração de PDF no backend.
- Download fiscal, XML ou SEFAZ.

## Testes automatizados

- Botão por status, spinner, sucesso, erros e chamada de impressão.

## Teste manual

- Imprimir nota aberta, confirmar visualização, saldo/status e bloqueio posterior.

## Commit previsto

`feat(frontend): add invoice printing workflow`

