using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;  // Para enviar solicitudes HTTP a Laravel
using System.Text;
using Demo;

class ComparacionHuellas
{
    private static DatabaseManager dbManager = new DatabaseManager();  // Constructor sin parámetros
    private static readonly HttpClient client = new HttpClient();  // Cliente HTTP para enviar notificaciones a Laravel

    static async Task Main(string[] args)
    {
        // Abrir el dispositivo de huellas
        int iRet = FPutils.FPModule_OpenDevice();
        if (iRet != FPutils.FP_SUCCESS)
        {
            Console.WriteLine("Error al abrir el dispositivo.");
            return;
        }

        // Comparación de huellas en tiempo real
        await CompararHuellasContinuamente();
    }

    static async Task CompararHuellasContinuamente()
    {
        while (true)
        {
            // Detectar si hay un dedo en el sensor
            int fingerDetected = 0;
            int iRet = FPutils.FPModule_DetectFinger(ref fingerDetected);

            if (iRet == FPutils.FP_SUCCESS && fingerDetected == 1)
            {
                // Usar FpEnroll para capturar la plantilla de la huella digital
                byte[] capturedTemplate = new byte[FPutils.FP_FTP_MAX];  // FP_FTP_MAX asegura el tamaño adecuado de la plantilla (512 bytes)
                FPutils.FPModule_SetCollectTimes(1);  // Intentos de captura para mayor precisión
                iRet = FPutils.FPModule_FpEnroll(capturedTemplate);

                if (iRet == FPutils.FP_SUCCESS)
                {
                    Console.WriteLine("Huella capturada para comparación.");
                    bool huellaCoincide = false;  // Variable para determinar si la huella coincide con algún cliente

                    // Obtener todos los IDs de clientes con huellas almacenadas en la base de datos
                    var clientes = dbManager.ObtenerClientesConHuella(); // Método para obtener una lista de IDs de clientes

                    foreach (var clienteId in clientes)
                    {
                        // Usar ruta absoluta para buscar el archivo de huella
                        string filePath = $@"C:\Users\James\Desktop\fingerprint\FingerprintApp\temp_{clienteId}.dat";
                        Console.WriteLine($"Buscando el archivo de huella: {filePath}");

                        if (File.Exists(filePath))
                        {
                            byte[] storedTemplate = File.ReadAllBytes(filePath);

                            // Comparar las huellas usando FPModule_MatchTemplate
                            int matchResult = FPutils.FPModule_MatchTemplate(storedTemplate, capturedTemplate, 3);

                            if (matchResult == FPutils.FP_SUCCESS)
                            {
                                // Verificar si el cliente tiene una suscripción activa
                                bool suscripcionActiva = dbManager.TieneSuscripcionActiva(clienteId);

                                if (suscripcionActiva)
                                {
                                    Console.WriteLine($"Acceso permitido. Huellas coinciden y el cliente ID: {clienteId} tiene una suscripción activa.");
                                    await EnviarNotificacionALaravel(clienteId, "permitido");  // Enviar notificación de éxito
                                }
                                else
                                {
                                    Console.WriteLine($"Acceso denegado. El cliente ID: {clienteId} tiene una suscripción inactiva.");
                                    await EnviarNotificacionALaravel(clienteId, "denegado");  // Notificación de suscripción inactiva
                                }

                                huellaCoincide = true;  // Establecer que la huella coincide
                                break;  // Si coincide con una huella, dejamos de comparar
                            }
                        }
                    }

                    // Si ninguna huella coincide, enviar notificación de acceso denegado
                    if (!huellaCoincide)
                    {
                        Console.WriteLine("Acceso denegado. Ninguna huella coincide.");
                        await EnviarNotificacionALaravel(null, "denegado");  // Enviar notificación de acceso denegado sin cliente específico
                    }
                }
                else
                {
                    Console.WriteLine("Error al enrolar la huella.");
                }
            }
            else
            {
                Console.WriteLine("No hay un dedo en el sensor.");
            }

            // Esperar antes de intentar detectar la huella nuevamente
            await Task.Delay(5000);  // Espera 5 segundos antes de la siguiente detección
        }
    }

    // Método para enviar la notificación a Laravel
    static async Task EnviarNotificacionALaravel(int? clienteId, string estadoAcceso)
    {
        var jsonContent = new
        {
            clienteId = clienteId,  // clienteId es nullable, puede ser null si es "denegado"
            estado = estadoAcceso
        };

        var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(jsonContent), Encoding.UTF8, "application/json");

        try
        {
            // Asumimos que tienes una ruta /api/notificaciones en Laravel para manejar esto
            HttpResponseMessage response = await client.PostAsync("http://localhost:8000/api/notificaciones", content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Notificación de acceso {estadoAcceso} enviada correctamente.");
            }
            else
            {
                Console.WriteLine("Error al enviar la notificación a Laravel.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al enviar notificación: {ex.Message}");
        }
    }
}


