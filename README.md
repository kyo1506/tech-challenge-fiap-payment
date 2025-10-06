# Tech Challenge FIAP - Módulo de Pagamentos

## 📖 Sumário

* [Sobre o Projeto](#-sobre-o-projeto)
* [✨ Funcionalidades](#-funcionalidades)
* [🏛️ Arquitetura](#️-arquitetura)
* [💻 Tecnologias Utilizadas](#-tecnologias-utilizadas)
* [⚙️ Estrutura do Projeto](#️-estrutura-do-projeto)

## ❔ Sobre o Projeto

Este projeto é uma solução de microsserviço para gerenciamento de pagamentos, desenvolvido como parte do Tech Challenge da FIAP. O serviço é responsável por controlar a carteira de pagamentos dos usuários, processar compras e estornos, utilizando uma arquitetura robusta e escalável com princípios de **CQRS (Command Query Responsibility Segregation)** e **Event Sourcing**.

## ✨ Funcionalidades

- **Gestão de Carteira (Wallet):**
  - Consultar saldo.
  - Consultar histórico de transações.
  - Depositar fundos.
  - Sacar fundos.
- **Processamento de Compras:**
  - Criação de novas compras.
  - Confirmação de pagamento de compras.
  - Consulta de status de compras.
- **Processamento de Estornos:**
  - Solicitação de estorno de compras.
- **Processador de Comandos (Lambda):**
  - Processamento assíncrono de comandos de `Purchase` e `Wallet` via filas SQS.

## 🏛️ Arquitetura

A arquitetura do projeto segue os princípios da **Clean Architecture**, dividindo o sistema em camadas de responsabilidades claras, promovendo baixo acoplamento e alta coesão.

- **Domain:** Contém as entidades de negócio, agregados, eventos de domínio e a lógica de negócio principal. 
- **Application:** Orquestra o fluxo de dados e as regras de aplicação. 
- **Infrastructure:** Implementa os detalhes de acesso a dados, como repositórios, comunicação com serviços externos (AWS SQS, SNS), e logging (Elasticsearch).
- **Presentation:** A camada de entrada da aplicação, neste caso, uma API RESTful construída com ASP.NET Core. É responsável por receber as requisições, validá-las e chamar os serviços da camada de aplicação.
- **Shared:** Contém DTOs, comandos e eventos que são compartilhados entre os projetos.

Além disso, o projeto utiliza os padrões **CQRS** e **Event Sourcing**:
- **CQRS:** Separa as operações de escrita (Commands) das operações de leitura (Queries). Isso permite otimizar cada lado de forma independente.
- **Event Sourcing:** Em vez de armazenar o estado atual das entidades, armazenamos a sequência de eventos que ocorreram. O estado atual é derivado a partir desses eventos.

## 💻 Tecnologias Utilizadas

- **.NET 8:** Framework para desenvolvimento da aplicação.
- **ASP.NET Core 8:** Para a construção da API.
- **Entity Framework Core:** ORM para persistência de dados.
- **Docker e Docker Compose:** Para containerização e orquestração do ambiente de desenvolvimento.
- **PostgreSQL:** Banco de dados relacional para o Event Store.
- **Serilog com Elasticsearch:** Para logging estruturado e centralizado.
- **AWS Services:**
  - **Lambda:** Para processamento assíncrono de comandos.
  - **SQS (Simple Queue Service):** Para filas de comandos.
  - **SNS (Simple Notification Service):** Para publicação de eventos.
- **xUnit:** Para a escrita de testes unitários.

## ⚙️ Estrutura do Projeto
```
/
├── Application/        # Camada de Aplicação (Casos de Uso, Serviços, Mappers)
├── Domain/             # Camada de Domínio (Entidades, Agregados, Eventos, Regras de Negócio)
├── Domain.Tests/       # Testes unitários para a camada de Domínio
├── Infrastructure/     # Camada de Infraestrutura (Repositórios, Mensageria, Acesso a Dados)
├── PaymentService.Processor/ # Projeto do AWS Lambda para processamento assíncrono
├── Presentation/       # Camada de Apresentação (API, Controllers, Middlewares)
├── Shared/             # DTOs, Comandos e Eventos compartilhados entre os projetos

```
