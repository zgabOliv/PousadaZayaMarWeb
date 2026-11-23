# Conectando o projeto ao Azure SQL Database

Arquitetura: Flutter / navegador -> API ASP.NET Core (seu PC) -> Azure SQL Database.
Os clientes nunca falam direto com o banco, só com a API.

## 1. Criar o banco no Azure (portal.azure.com)
1. Crie uma conta em azure.microsoft.com/free (ou use o plano de estudante, se tiver).
   A oferta gratuita do SQL Database não funciona com o plano "Azure for Students Starter".
2. Criar um recurso > **SQL Database**.
3. Aba Básico:
   - Grupo de recursos: novo (ex.: `rg-pousada`).
   - Nome do banco: `PousadaMareMansa`.
   - Servidor: **Criar novo**, nome único (ex.: `pousada-mare-mansa-SEUNOME`), autenticação **SQL**,
     administrador (ex.: `adminpousada`) e uma senha forte. **Anote os dois.**
   - Clique em **Apply offer** no banner "Want to try Azure SQL Database for free?".
     Em "Behavior when free limit reached", escolha pausar o banco até o mês seguinte (evita cobrança).
     Se o banner não aparecer, troque a região.
4. Aba Rede: ponto de extremidade **público**; marque **Adicionar o endereço IP do cliente atual** = Sim
   e **Permitir que serviços do Azure acessem o servidor** = Sim.
5. Revisar + criar. O custo estimado deve aparecer como 0.

Se seu IP mudar (internet residencial), adicione o novo em Servidor > Rede > Regras de firewall.

## 2. Criar as tabelas (SSMS)
1. Conecte em `SEU-SERVIDOR.database.windows.net`, autenticação **SQL Server**, usuário administrador.
2. Em Opções > Propriedades de Conexão, em "Conectar ao banco de dados" escreva `PousadaMareMansa`.
3. Abra `Database/azure_sql_criar_banco.sql`, troque as duas senhas no final e execute tudo.

## 3. Apontar o projeto para o Azure (sem gravar senha no código)
No terminal, na pasta do projeto (troque SEU-SERVIDOR e as senhas):

    dotnet user-secrets set "ConnectionStrings:Site" "Server=tcp:SEU-SERVIDOR.database.windows.net,1433;Initial Catalog=PousadaMareMansa;User ID=app_site;Password=SENHA-SITE;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;ConnectRetryCount=5;ConnectRetryInterval=10" --project PousadaMareMansa.Web.csproj
    dotnet user-secrets set "ConnectionStrings:Admin" "Server=tcp:SEU-SERVIDOR.database.windows.net,1433;Initial Catalog=PousadaMareMansa;User ID=app_admin;Password=SENHA-ADMIN;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;ConnectRetryCount=5;ConnectRetryInterval=10" --project PousadaMareMansa.Web.csproj
    dotnet user-secrets set "Jwt:Key" "UMA-CHAVE-NOVA-COM-MAIS-DE-32-CARACTERES" --project PousadaMareMansa.Web.csproj

No Visual Studio: botão direito no projeto > **Gerenciar Segredos do Usuário** (cole o JSON equivalente).
Os segredos valem no ambiente Development e substituem o appsettings.json.
Para voltar ao SQL Server local: `dotnet user-secrets clear --project PousadaMareMansa.Web.csproj`.

## 4. Testar
1. Rode o site (F5) e abra `/swagger`.
2. `GET /api/v1/quartos` deve devolver os 4 tipos de quarto vindos do Azure.
3. `/health/ready` deve responder Healthy.
4. Crie o administrador: `dotnet run --project PousadaMareMansa.Web.csproj -- --criar-admin gerente SuaSenha Gerente`
   e teste `POST /api/v1/auth/login`.

## Atenção
- O plano gratuito é serverless e **pausa** quando ninguém usa: a primeira chamada depois da pausa pode levar cerca de um minuto (por isso o `Connection Timeout=60`).
- Limite gratuito: 100 mil vCore-segundos de computação e 32 GB por mês, por banco.
- Azure SQL guarda horários em UTC (DataCriacao, DataPagamento, LogReserva). As regras de data usam o horário de Brasília.
- Nunca use `Encrypt=False` nem `TrustServerCertificate=True` com o Azure. Isso era só para o SQL Server local.
- Não versione senhas. Apague a regra de firewall do seu IP quando não estiver usando.

---

# Parte 2: publicar a API no Azure App Service e conectar o app

Arquitetura final: App Flutter (celular) -> HTTPS -> App Service (site + API) -> Azure SQL Database.

## 5. Criar o App Service (portal.azure.com)
1. Criar um recurso > **Aplicativo Web (Web App)**.
2. Grupo de recursos: o mesmo do banco (`rg-pousada`). Nome: único (vira `https://NOME.azurewebsites.net`).
3. Publicar: **Código**. Pilha de runtime: **.NET 8 (LTS)**. Sistema operacional: Windows ou Linux
   (use o que aceitar o plano **Free F1**). Região: a mesma do banco, se possível.
4. Plano de preços: **Free F1** (gratuito) ou Basic B1 (pago, mais estável).
5. Criar.

Limites do F1: cota de CPU por dia (cerca de 60 minutos). Se estourar, o site fica fora do ar e responde 403 até a cota
reiniciar (meia-noite UTC). Não tem "Always On": depois de ficar parado, a primeira chamada demora para acordar.

## 6. Configurar o App Service
No App Service: **Configurações > Variáveis de ambiente** (ou "Configuração") > **Configurações do aplicativo**. Adicione:

| Nome | Valor |
|---|---|
| ConnectionStrings__Site | a mesma connection string do `app_site` (Azure SQL) |
| ConnectionStrings__Admin | a mesma connection string do `app_admin` |
| Jwt__Key | chave com 32+ caracteres (sem ela o site não inicia) |
| Swagger__Enabled | true (opcional: mostra /swagger na apresentação) |

Salve. Depois, em **Configuração geral** (ou Configurações > TLS/SSL), ative **Somente HTTPS**.

## 7. Publicar o código (na pasta do projeto, no CMD)
    dotnet publish PousadaMareMansa.Web.csproj -c Release -o publicar
    tar -a -c -f publicar.zip -C publicar .
    az login
    az webapp deploy --resource-group rg-pousada --name NOME-DO-APP --src-path publicar.zip --type zip

(Precisa do Azure CLI: `winget install Microsoft.AzureCLI`. Alternativa: no Visual Studio, Compilar > Publicar > Azure > Serviço de Aplicativo.)

## 8. Testar a API publicada
- `https://NOME-DO-APP.azurewebsites.net/api/v1/quartos` deve listar os 4 quartos.
- `https://NOME-DO-APP.azurewebsites.net/health/ready` deve dizer Healthy.
- `https://NOME-DO-APP.azurewebsites.net/swagger` (se Swagger__Enabled=true).
- A 1ª chamada pode levar mais de 30 s (acorda o site e o banco).
- Administrador: crie localmente com as user-secrets apontando para o Azure SQL
  (`dotnet run --project PousadaMareMansa.Web.csproj -- --criar-admin gerente SuaSenha Gerente`).

## 9. Conectar o app
1. Gere o APK de novo com o código novo (tem a tela de servidor e espera de 60 s) e instale.
2. No app, toque na engrenagem (canto superior direito), digite `https://NOME-DO-APP.azurewebsites.net`
   e toque em **Testar e salvar**.
3. Faça uma reserva pelo app e confira o resultado no painel do site e no banco (tabela Reserva).

Se der erro: veja **Fluxo de log** do App Service (Monitoramento) e confira os nomes das variáveis (dois sublinhados).
