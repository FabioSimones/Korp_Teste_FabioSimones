# Requisitos do desafio

## Objetivo

Desenvolver uma aplicação Angular com backend C# em microsserviços para cadastro de produtos, cadastro de notas e impressão com baixa de estoque.

## Obrigatórios

### Produtos

- Cadastrar produto com código, descrição e saldo.
- Código obrigatório e único.
- Descrição obrigatória.
- Saldo inteiro maior ou igual a zero.
- Persistir os dados fisicamente.

### Notas fiscais

- Gerar numeração sequencial no backend.
- Criar nota com status inicial `Open`.
- Permitir múltiplos produtos e respectivas quantidades.
- Aceitar somente quantidades inteiras positivas.
- Persistir nota e itens fisicamente.

### Impressão

- Exibir botão de impressão visível.
- Mostrar indicador durante o processamento.
- Permitir impressão somente de nota aberta.
- Atualizar a nota para `Closed` após sucesso.
- Descontar as quantidades dos saldos.
- Não deixar saldo negativo.
- Disponibilizar visualização adequada para impressão.

### Arquitetura

- Pelo menos dois microsserviços.
- Estoque controla produtos e saldos.
- Faturamento controla notas e impressão.
- Cada serviço é proprietário dos seus dados.

### Falhas

- Demonstrar indisponibilidade de um microsserviço.
- Fornecer mensagem adequada ao usuário.
- Manter a nota aberta quando a baixa não for confirmada.
- Permitir recuperação e nova tentativa.

## Opcionais priorizados

- Idempotência da baixa por `OperationId`.
- Concorrência para impedir consumo duplo do último item.

## Não será implementado

- Autenticação e autorização.
- Clientes e fornecedores.
- Valores, tributos ou pagamentos.
- XML de NF-e, SEFAZ ou certificado digital.
- Cancelamento, devolução ou estorno.
- Dashboard ou relatórios complexos.
- Inteligência artificial, salvo decisão posterior após todo o obrigatório.

## Entrega

- Repositório público `Korp_Teste_FabioSimones`.
- Vídeo demonstrando telas, funcionalidades e solução técnica.
- Documento de detalhamento técnico.

