# Galeria completa de capturas de tela

Galeria estendida — o `README.md` traz apenas as 6 imagens principais (seção "Visão do sistema").
Ver `docs/images/screenshots/README.md` para a convenção de nomes e o que ainda falta ser
adicionado fisicamente.

## Produtos

### Consulta de produtos

![Listagem de produtos cadastrados, com paginação e busca por código/descrição](images/screenshots/products-list.png)

Listagem paginada de produtos (`Inventory.Api`), com colunas ordenáveis (código, descrição, saldo)
e controle de itens por página.

### Cadastro de produto — formulário vazio

![Modal de cadastro de produto com os campos código, descrição e saldo vazios](images/screenshots/product-form-dialog.png)

Modal de cadastro (Angular Material `MatDialog`). O campo de saldo inicial começa vazio — decisão
tomada na Task 15, já que `0` deixou de ser um valor inicial válido.

### Cadastro de produto — validação inline

![Modal de cadastro de produto exibindo mensagens de validação em vermelho para os três campos obrigatórios](images/screenshots/product-form-dialog-validation.png)

Validação client-side antes de qualquer chamada HTTP: código, descrição e saldo inicial
obrigatórios, com o botão "Cadastrar produto" desabilitado enquanto o formulário for inválido.

### Cadastro de produto — sucesso

![Listagem de produtos com um toast verde confirmando o cadastro do produto 123564](images/screenshots/product-create-success.png)

Após `POST /api/products` retornar `201 Created`, o modal fecha e a listagem é atualizada com o
novo produto; um único toast de sucesso é exibido.

## Notas fiscais

### Consulta de notas fiscais

![Listagem de notas fiscais, com número, emissão, itens e status](images/screenshots/invoices-list.png)

Listagem paginada de notas (`Billing.Api`), com badge de status (`ABERTA`/`FECHADA`) e ordenação
por número/emissão.

### Criação de nota — formulário vazio

![Modal "Nova nota fiscal" recém-aberto, sem nenhum item adicionado](images/screenshots/invoice-form-dialog-empty.png)

Estado inicial do modal de criação de nota, antes de qualquer item ser adicionado.

### Criação de nota — um item

![Modal "Nova nota fiscal" com uma linha de item, campos de produto e quantidade](images/screenshots/invoice-form-dialog-single-item.png)

Uma linha de item adicionada via "Adicionar item", com seleção de produto e quantidade.

### Criação de nota — múltiplos itens

![Modal "Nova nota fiscal" com dois itens preenchidos, mostrando o saldo disponível de cada produto e o botão Criar nota habilitado](images/screenshots/invoice-form-dialog.png)

Formulário válido com múltiplos itens (`FormArray`), exibindo o saldo disponível de cada produto
selecionado e o botão "Criar nota" habilitado.

### Detalhe da nota — aberta

![Detalhe de uma nota fiscal com status ABERTA e o botão Imprimir e fechar nota](images/screenshots/invoice-detail-open.png)

Nota recém-criada, status `ABERTA`, com aviso de que a impressão fecha a nota e realiza a baixa no
estoque (ação que não pode ser desfeita).

### Detalhe da nota — saldo insuficiente

![Detalhe de uma nota fiscal com um toast vermelho indicando que o produto não possui saldo suficiente](images/screenshots/invoice-detail-insufficient-stock.png)

Tentativa de impressão rejeitada com `409` porque a quantidade solicitada excede o saldo
disponível do produto — a nota permanece `ABERTA` e o saldo não é alterado.

### Detalhe da nota — fechada

![Detalhe de uma nota fiscal com status FECHADA, data de fechamento e botão de impressão desabilitado](images/screenshots/invoice-detail.png)

Após a impressão bem-sucedida: status `FECHADA`, data de fechamento preenchida e o botão
"Imprimir e fechar nota" desabilitado (não é possível reimprimir pela UI).

## Impressão

### Visualização preparada para impressão

*(Pendente — ver `docs/images/screenshots/README.md`. Nenhuma captura de tela da visualização
`invoice-print-view` foi fornecida na Task 16.)*
