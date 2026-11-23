/* ============================================================
   Zaya Mar Pousada, versão 2, parte 1:
   contas de hóspede (login, e-mail confirmado, recuperação de senha, bloqueio por tentativas),
   avaliações públicas, página e calendário de cada suíte.
   COMO RODAR: no SSMS, conectado ao banco PousadaMareMansa com o administrador do servidor.
   Pode rodar mais de uma vez (reexecutável). Rode ANTES de publicar o site novo.
   ============================================================ */

IF COL_LENGTH('TipoQuarto', 'Imagem') IS NULL ALTER TABLE TipoQuarto ADD Imagem NVARCHAR(200) NULL;
GO

IF OBJECT_ID('ContaHospede', 'U') IS NULL
CREATE TABLE ContaHospede (
    IdHospede        INT NOT NULL CONSTRAINT PK_ContaHospede PRIMARY KEY
                         CONSTRAINT FK_Conta_Hospede REFERENCES Hospede(IdHospede),
    SenhaHash        VARBINARY(64) NOT NULL,
    Salt             VARBINARY(32) NOT NULL,
    EmailConfirmado  BIT NOT NULL CONSTRAINT DF_Conta_Conf DEFAULT 0,
    TentativasFalhas TINYINT NOT NULL CONSTRAINT DF_Conta_Tent DEFAULT 0,
    BloqueadoAte     DATETIME2(0) NULL,
    UltimoLogin      DATETIME2(0) NULL,
    DataCriacao      DATETIME2(0) NOT NULL CONSTRAINT DF_Conta_Data DEFAULT SYSUTCDATETIME()
);

IF OBJECT_ID('TokenConta', 'U') IS NULL
CREATE TABLE TokenConta (
    IdToken   INT IDENTITY(1,1) CONSTRAINT PK_TokenConta PRIMARY KEY,
    IdHospede INT NOT NULL CONSTRAINT FK_Token_Hospede REFERENCES Hospede(IdHospede),
    Tipo      VARCHAR(10) NOT NULL CONSTRAINT CK_Token_Tipo CHECK (Tipo IN ('confirmar','redefinir')),
    TokenHash VARBINARY(32) NOT NULL,          -- só o hash do link fica no banco
    Expira    DATETIME2(0) NOT NULL,
    Usado     BIT NOT NULL CONSTRAINT DF_Token_Usado DEFAULT 0
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Token_Hash' AND object_id = OBJECT_ID('TokenConta'))
CREATE INDEX IX_Token_Hash ON TokenConta (TokenHash) INCLUDE (Tipo, Expira, Usado, IdHospede);

IF OBJECT_ID('EmailSaida', 'U') IS NULL
CREATE TABLE EmailSaida (
    IdEmail     INT IDENTITY(1,1) CONSTRAINT PK_EmailSaida PRIMARY KEY,
    Para        NVARCHAR(160) NOT NULL,
    Assunto     NVARCHAR(200) NOT NULL,
    Corpo       NVARCHAR(MAX) NOT NULL,
    DataCriacao DATETIME2(0) NOT NULL CONSTRAINT DF_Email_Data DEFAULT SYSUTCDATETIME(),
    Enviado     BIT NOT NULL CONSTRAINT DF_Email_Env DEFAULT 0
);
GO

/* ---------- conta ---------- */
CREATE OR ALTER PROCEDURE sp_CadastrarConta
    @Nome NVARCHAR(120), @Email NVARCHAR(160), @Telefone VARCHAR(20),
    @Hash VARBINARY(64), @Salt VARBINARY(32), @IdHospede INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRAN;
        SELECT @IdHospede = IdHospede FROM Hospede WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email;
        IF @IdHospede IS NOT NULL AND EXISTS (SELECT 1 FROM ContaHospede WHERE IdHospede = @IdHospede)
            THROW 50020, 'Já existe uma conta com este e-mail. Use Entrar ou Esqueci a senha.', 1;
        IF @IdHospede IS NULL
        BEGIN
            INSERT INTO Hospede (Nome, Email, Telefone) VALUES (@Nome, @Email, @Telefone);
            SET @IdHospede = SCOPE_IDENTITY();
        END
        INSERT INTO ContaHospede (IdHospede, SenhaHash, Salt) VALUES (@IdHospede, @Hash, @Salt);
    COMMIT;
END;
GO

-- EXECUTE AS OWNER: devolve telefone e e-mail sem máscara (o usuário do site só vê dados mascarados)
CREATE OR ALTER PROCEDURE sp_ObterContaPorEmail @Email NVARCHAR(160)
WITH EXECUTE AS OWNER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT h.IdHospede, h.Nome, h.Email, h.Telefone, c.SenhaHash, c.Salt, c.EmailConfirmado, c.BloqueadoAte, c.TentativasFalhas
    FROM Hospede h JOIN ContaHospede c ON c.IdHospede = h.IdHospede
    WHERE h.Email = @Email;
END;
GO

CREATE OR ALTER PROCEDURE sp_PerfilHospede @IdHospede INT
WITH EXECUTE AS OWNER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT h.IdHospede, h.Nome, h.Email, h.Telefone, c.EmailConfirmado
    FROM Hospede h JOIN ContaHospede c ON c.IdHospede = h.IdHospede
    WHERE h.IdHospede = @IdHospede;
END;
GO

-- 5 senhas erradas seguidas bloqueiam o login por 15 minutos
CREATE OR ALTER PROCEDURE sp_RegistrarLogin @IdHospede INT, @Sucesso BIT AS
BEGIN
    SET NOCOUNT ON;
    IF @Sucesso = 1
        UPDATE ContaHospede SET TentativasFalhas = 0, BloqueadoAte = NULL, UltimoLogin = SYSUTCDATETIME()
        WHERE IdHospede = @IdHospede;
    ELSE
        UPDATE ContaHospede SET
            TentativasFalhas = CASE WHEN TentativasFalhas + 1 >= 5 THEN 0 ELSE TentativasFalhas + 1 END,
            BloqueadoAte     = CASE WHEN TentativasFalhas + 1 >= 5 THEN DATEADD(MINUTE, 15, SYSUTCDATETIME()) ELSE BloqueadoAte END
        WHERE IdHospede = @IdHospede;
END;
GO

CREATE OR ALTER PROCEDURE sp_CriarToken @IdHospede INT, @Tipo VARCHAR(10), @TokenHash VARBINARY(32), @Minutos INT AS
BEGIN
    SET NOCOUNT ON;
    UPDATE TokenConta SET Usado = 1 WHERE IdHospede = @IdHospede AND Tipo = @Tipo AND Usado = 0;
    INSERT INTO TokenConta (IdHospede, Tipo, TokenHash, Expira)
    VALUES (@IdHospede, @Tipo, @TokenHash, DATEADD(MINUTE, @Minutos, SYSUTCDATETIME()));
END;
GO

CREATE OR ALTER PROCEDURE sp_ConfirmarEmail @TokenHash VARBINARY(32) AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @Id INT;
    BEGIN TRAN;
        SELECT TOP (1) @Id = IdHospede FROM TokenConta WITH (UPDLOCK)
        WHERE TokenHash = @TokenHash AND Tipo = 'confirmar' AND Usado = 0 AND Expira > SYSUTCDATETIME();
        IF @Id IS NULL THROW 50021, 'Link inválido ou expirado. Peça um novo.', 1;
        UPDATE TokenConta SET Usado = 1 WHERE TokenHash = @TokenHash AND Tipo = 'confirmar';
        UPDATE ContaHospede SET EmailConfirmado = 1 WHERE IdHospede = @Id;
    COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE sp_RedefinirSenha @TokenHash VARBINARY(32), @Hash VARBINARY(64), @Salt VARBINARY(32) AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @Id INT;
    BEGIN TRAN;
        SELECT TOP (1) @Id = IdHospede FROM TokenConta WITH (UPDLOCK)
        WHERE TokenHash = @TokenHash AND Tipo = 'redefinir' AND Usado = 0 AND Expira > SYSUTCDATETIME();
        IF @Id IS NULL THROW 50021, 'Link inválido ou expirado. Peça um novo.', 1;
        UPDATE TokenConta SET Usado = 1 WHERE TokenHash = @TokenHash AND Tipo = 'redefinir';
        UPDATE ContaHospede SET SenhaHash = @Hash, Salt = @Salt, TentativasFalhas = 0, BloqueadoAte = NULL, EmailConfirmado = 1
        WHERE IdHospede = @Id;
    COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE sp_RegistrarEmail @Para NVARCHAR(160), @Assunto NVARCHAR(200), @Corpo NVARCHAR(MAX), @IdEmail INT OUTPUT AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO EmailSaida (Para, Assunto, Corpo) VALUES (@Para, @Assunto, @Corpo);
    SET @IdEmail = SCOPE_IDENTITY();
END;
GO

CREATE OR ALTER PROCEDURE sp_MarcarEmailEnviado @IdEmail INT AS
BEGIN
    SET NOCOUNT ON;
    UPDATE EmailSaida SET Enviado = 1 WHERE IdEmail = @IdEmail;
END;
GO

/* ---------- reservas da conta ---------- */
CREATE OR ALTER PROCEDURE sp_MinhasReservas @IdHospede INT AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM ContaHospede WHERE IdHospede = @IdHospede AND EmailConfirmado = 1)
        THROW 50022, 'Confirme seu e-mail para ver suas reservas.', 1;
    DECLARE @Hoje DATE = CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'E. South America Standard Time' AS DATE);
    SELECT r.Codigo, t.Nome AS Tipo, r.DataEntrada, r.DataSaida,
           DATEDIFF(DAY, r.DataEntrada, r.DataSaida) AS Noites, r.QtdHospedes, r.ValorTotal,
           ISNULL((SELECT SUM(p.Valor) FROM Pagamento p WHERE p.IdReserva = r.IdReserva AND p.Status = 'Aprovado'), 0) AS ValorPago,
           r.Status,
           CAST(CASE WHEN a.IdAvaliacao IS NULL THEN 0 ELSE 1 END AS BIT) AS Avaliada,
           CAST(CASE WHEN a.IdAvaliacao IS NULL AND r.Status IN ('Confirmada','Concluida') AND r.DataEntrada <= @Hoje THEN 1 ELSE 0 END AS BIT) AS PodeAvaliar
    FROM Reserva r
    JOIN Quarto q ON q.IdQuarto = r.IdQuarto
    JOIN TipoQuarto t ON t.IdTipoQuarto = q.IdTipoQuarto
    LEFT JOIN Avaliacao a ON a.IdReserva = r.IdReserva
    WHERE r.IdHospede = @IdHospede
    ORDER BY r.DataEntrada DESC;
END;
GO

CREATE OR ALTER PROCEDURE sp_CancelarReservaConta @IdHospede INT, @Codigo VARCHAR(10), @Gratuito BIT OUTPUT AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM Reserva WHERE Codigo = @Codigo AND IdHospede = @IdHospede)
        THROW 50006, 'Reserva não encontrada.', 1;
    EXEC sp_CancelarReserva @Codigo, @Gratuito OUTPUT;
END;
GO

/* ---------- avaliações ---------- */
CREATE OR ALTER PROCEDURE sp_AvaliarReserva @IdHospede INT, @Codigo VARCHAR(10), @Nota TINYINT, @Comentario NVARCHAR(500) AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Id INT;
    SELECT @Id = IdReserva FROM Reserva WHERE Codigo = @Codigo AND IdHospede = @IdHospede;
    IF @Id IS NULL THROW 50006, 'Reserva não encontrada.', 1;
    IF EXISTS (SELECT 1 FROM Avaliacao WHERE IdReserva = @Id) THROW 50023, 'Esta estadia já foi avaliada.', 1;
    INSERT INTO Avaliacao (IdReserva, Nota, Comentario) VALUES (@Id, @Nota, NULLIF(LTRIM(RTRIM(@Comentario)), ''));
END;
GO

CREATE OR ALTER PROCEDURE sp_ListarAvaliacoes @IdTipoQuarto INT = NULL, @Top INT = 6 AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top) a.Nota, a.Comentario,
           LEFT(h.Nome, CHARINDEX(' ', h.Nome + ' ') - 1) AS Autor,   -- só o primeiro nome
           a.DataAval, t.Nome AS Tipo
    FROM Avaliacao a
    JOIN Reserva r ON r.IdReserva = a.IdReserva
    JOIN Hospede h ON h.IdHospede = r.IdHospede
    JOIN Quarto q ON q.IdQuarto = r.IdQuarto
    JOIN TipoQuarto t ON t.IdTipoQuarto = q.IdTipoQuarto
    WHERE a.Comentario IS NOT NULL AND (@IdTipoQuarto IS NULL OR t.IdTipoQuarto = @IdTipoQuarto)
    ORDER BY a.DataAval DESC;
END;
GO

CREATE OR ALTER PROCEDURE sp_ResumoAvaliacoes @IdTipoQuarto INT = NULL AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(AVG(CAST(a.Nota AS DECIMAL(4,2))) AS DECIMAL(3,2)) AS Media, COUNT(*) AS Total
    FROM Avaliacao a
    JOIN Reserva r ON r.IdReserva = a.IdReserva
    JOIN Quarto q ON q.IdQuarto = r.IdQuarto
    WHERE @IdTipoQuarto IS NULL OR q.IdTipoQuarto = @IdTipoQuarto;
END;
GO

/* ---------- suítes ---------- */
CREATE OR ALTER PROCEDURE sp_ListarTiposQuarto AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.IdTipoQuarto, t.Nome, t.Capacidade, t.PrecoDiaria, t.Acessivel, t.Descricao, t.Imagem,
           (SELECT COUNT(*) FROM Quarto q WHERE q.IdTipoQuarto = t.IdTipoQuarto AND q.Ativo = 1) AS Unidades,
           (SELECT STRING_AGG(c.Nome, ', ') FROM TipoQuartoComodidade x
            JOIN Comodidade c ON c.IdComodidade = x.IdComodidade WHERE x.IdTipoQuarto = t.IdTipoQuarto) AS Comodidades,
           (SELECT CAST(AVG(CAST(a.Nota AS DECIMAL(4,2))) AS DECIMAL(3,2)) FROM Avaliacao a
            JOIN Reserva r ON r.IdReserva = a.IdReserva JOIN Quarto q2 ON q2.IdQuarto = r.IdQuarto
            WHERE q2.IdTipoQuarto = t.IdTipoQuarto) AS Media,
           (SELECT COUNT(*) FROM Avaliacao a2
            JOIN Reserva r2 ON r2.IdReserva = a2.IdReserva JOIN Quarto q3 ON q3.IdQuarto = r2.IdQuarto
            WHERE q3.IdTipoQuarto = t.IdTipoQuarto) AS TotalAvaliacoes
    FROM TipoQuarto t
    ORDER BY t.PrecoDiaria;
END;
GO

CREATE OR ALTER PROCEDURE sp_DetalheTipoQuarto @IdTipoQuarto INT AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.IdTipoQuarto, t.Nome, t.Capacidade, t.PrecoDiaria, t.Acessivel, t.Descricao, t.Imagem,
           (SELECT COUNT(*) FROM Quarto q WHERE q.IdTipoQuarto = t.IdTipoQuarto AND q.Ativo = 1) AS Unidades,
           (SELECT STRING_AGG(c.Nome, ', ') FROM TipoQuartoComodidade x
            JOIN Comodidade c ON c.IdComodidade = x.IdComodidade WHERE x.IdTipoQuarto = t.IdTipoQuarto) AS Comodidades,
           (SELECT CAST(AVG(CAST(a.Nota AS DECIMAL(4,2))) AS DECIMAL(3,2)) FROM Avaliacao a
            JOIN Reserva r ON r.IdReserva = a.IdReserva JOIN Quarto q2 ON q2.IdQuarto = r.IdQuarto
            WHERE q2.IdTipoQuarto = t.IdTipoQuarto) AS Media,
           (SELECT COUNT(*) FROM Avaliacao a2
            JOIN Reserva r2 ON r2.IdReserva = a2.IdReserva JOIN Quarto q3 ON q3.IdQuarto = r2.IdQuarto
            WHERE q3.IdTipoQuarto = t.IdTipoQuarto) AS TotalAvaliacoes
    FROM TipoQuarto t
    WHERE t.IdTipoQuarto = @IdTipoQuarto;
END;
GO

-- Para cada dia do mês: quantas suítes do tipo estão livres naquela noite
CREATE OR ALTER PROCEDURE sp_CalendarioTipo @IdTipoQuarto INT, @Mes DATE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Ini DATE = DATEFROMPARTS(YEAR(@Mes), MONTH(@Mes), 1);
    DECLARE @Fim DATE = DATEADD(MONTH, 1, @Ini);
    DECLARE @Total INT = (SELECT COUNT(*) FROM Quarto WHERE IdTipoQuarto = @IdTipoQuarto AND Ativo = 1);
    WITH d AS (
        SELECT @Ini AS Dia
        UNION ALL
        SELECT DATEADD(DAY, 1, Dia) FROM d WHERE DATEADD(DAY, 1, Dia) < @Fim
    )
    SELECT d.Dia,
           @Total - (SELECT COUNT(*) FROM Reserva r JOIN Quarto q ON q.IdQuarto = r.IdQuarto
                     WHERE q.IdTipoQuarto = @IdTipoQuarto AND r.Status <> 'Cancelada'
                       AND r.DataEntrada <= d.Dia AND r.DataSaida > d.Dia) AS Livres
    FROM d
    OPTION (MAXRECURSION 40);
END;
GO

/* ---------- permissões do site ---------- */
GRANT EXECUTE ON sp_CadastrarConta TO rl_site;
GRANT EXECUTE ON sp_ObterContaPorEmail TO rl_site;
GRANT EXECUTE ON sp_PerfilHospede TO rl_site;
GRANT EXECUTE ON sp_RegistrarLogin TO rl_site;
GRANT EXECUTE ON sp_CriarToken TO rl_site;
GRANT EXECUTE ON sp_ConfirmarEmail TO rl_site;
GRANT EXECUTE ON sp_RedefinirSenha TO rl_site;
GRANT EXECUTE ON sp_RegistrarEmail TO rl_site;
GRANT EXECUTE ON sp_MarcarEmailEnviado TO rl_site;
GRANT EXECUTE ON sp_MinhasReservas TO rl_site;
GRANT EXECUTE ON sp_CancelarReservaConta TO rl_site;
GRANT EXECUTE ON sp_AvaliarReserva TO rl_site;
GRANT EXECUTE ON sp_ListarAvaliacoes TO rl_site;
GRANT EXECUTE ON sp_ResumoAvaliacoes TO rl_site;
GRANT EXECUTE ON sp_DetalheTipoQuarto TO rl_site;
GRANT EXECUTE ON sp_CalendarioTipo TO rl_site;
DENY SELECT, INSERT, UPDATE, DELETE ON ContaHospede TO rl_site;
DENY SELECT, INSERT, UPDATE, DELETE ON TokenConta TO rl_site;
DENY SELECT, INSERT, UPDATE, DELETE ON EmailSaida TO rl_site;
GO

/* Fotos das suítes (opcional): coloque o arquivo em wwwroot/img/suites/ e rode, por exemplo:
   UPDATE TipoQuarto SET Imagem = '/img/suites/brisa.jpg' WHERE Nome = N'Suíte Brisa';  */
PRINT 'Versão 2, parte 1 aplicada com sucesso.';
