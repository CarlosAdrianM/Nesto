-- Nesto#537 (épica NestoAPI#669): la franja «Puede ofrecer» de la plantilla de venta se puede plegar, y se recuerda por
-- usuario en el parámetro SugerenciasPlantillaPlegadas ("1" = plegada; cualquier otro valor o sin fila = desplegada).
--
-- Hace falta la fila '(defecto)': al leer, ParametrosUsuarioController copia la de (defecto) al usuario, y el PUT con el
-- que Nesto guarda el valor solo modifica filas que ya existen. Sin esta fila, la franja funciona igual (sale desplegada),
-- pero no se recuerda que la han plegado.
--
-- BD: NV (NestoConnection). Sin GRANTs (INSERT de datos). Se puede lanzar antes o después de publicar Nesto.

SET NOCOUNT ON;
USE NV;

IF NOT EXISTS (SELECT 1 FROM ParametrosUsuario
               WHERE Empresa = '1' AND Clave = 'SugerenciasPlantillaPlegadas' AND Usuario = '(defecto)')
BEGIN
    INSERT INTO ParametrosUsuario (Empresa, Clave, Usuario, Valor, Usuario2, [Fecha Modificación])
    VALUES ('1', 'SugerenciasPlantillaPlegadas', '(defecto)', '0', SYSTEM_USER, GETDATE());
END
GO

-- VERIFICACIÓN (debe devolver 1 fila con Valor = '0'):
SELECT Empresa, Clave, Usuario, Valor FROM ParametrosUsuario
WHERE Clave = 'SugerenciasPlantillaPlegadas' ORDER BY Usuario;
GO
