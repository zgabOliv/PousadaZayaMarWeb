/* Corrige reservas que receberam pagamento MAIOR que o valor total (erro de digitação).
   1) Rode o SELECT para ver o que será corrigido.  2) Rode o UPDATE para estornar o excesso. */

-- 1) Reservas com pagamento acima do total
SELECT r.Codigo, r.ValorTotal, SUM(p.Valor) AS TotalPago
FROM Reserva r JOIN Pagamento p ON p.IdReserva = r.IdReserva AND p.Status = 'Aprovado'
GROUP BY r.Codigo, r.ValorTotal
HAVING SUM(p.Valor) > r.ValorTotal;

-- 2) Estorna o pagamento digitado errado da reserva MM-5AB9E (confira o IdPagamento no SELECT abaixo antes)
SELECT p.IdPagamento, p.Valor, p.Forma, p.Status, p.DataPagamento
FROM Pagamento p JOIN Reserva r ON r.IdReserva = p.IdReserva WHERE r.Codigo = 'MM-5AB9E';

-- Troque 0 pelo IdPagamento do valor errado (R$ 14.556,00) e rode:
-- UPDATE Pagamento SET Status = 'Estornado' WHERE IdPagamento = 0;
-- Depois, se a reserva estiver certa com R$ 300,00 pagos, ela continua Confirmada.
