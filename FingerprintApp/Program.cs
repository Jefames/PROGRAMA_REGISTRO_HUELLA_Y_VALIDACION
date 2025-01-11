using System;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using Demo;
using System.IO;
using Newtonsoft.Json.Linq;  // Añadimos la referencia a Newtonsoft.Json para manejar JSON

class Program
{
    static void Main(string[] args)
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8000/"); // Usar otro puerto para este servicio de enrolamiento
        listener.Start();
        Console.WriteLine("Esperando solicitud de enrolamiento...");

        // Abrir el dispositivo de huellas
        int iRet = FPutils.FPModule_OpenDevice();
        if (iRet != FPutils.FP_SUCCESS)
        {
            Console.WriteLine("Error al abrir el dispositivo.");
            return;
        }

        // Escuchar solicitudes de Laravel para enrolar huellas
        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            Console.WriteLine("Solicitud de enrolamiento recibida");

            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            if (request.HttpMethod == "POST")
            {
                // Leer el cuerpo de la solicitud (que contiene JSON)
                string requestBody = new StreamReader(request.InputStream).ReadToEnd();
                
                // Parsear el JSON para obtener el ID del cliente
                JObject json = JObject.Parse(requestBody);
                string? clienteId = json["id"]?.ToString(); // Usar operador seguro para posibles valores NULL

                // Asegurarse de que el ID es válido antes de continuar
                if (string.IsNullOrEmpty(clienteId))
                {
                    byte[] errorBuffer = Encoding.UTF8.GetBytes("Error: ID del cliente no válido.");  // Usar 'errorBuffer'
                    response.ContentLength64 = errorBuffer.Length;
                    response.OutputStream.Write(errorBuffer, 0, errorBuffer.Length);
                    response.OutputStream.Close();
                    continue;
                }

                // Preparar la captura de la huella digital usando el SDK
                byte[] finalTemplate = new byte[FPutils.FP_FTP_MAX]; // Tamaño adecuado para la plantilla (512 bytes)
                FPutils.FPModule_SetCollectTimes(3); // Intentos de captura

                // Capturar la huella con el SDK (FPModule_FPEnroll)
                iRet = FPutils.FPModule_FpEnroll(finalTemplate);

                if (iRet == FPutils.FP_SUCCESS)
                {
                    // Verificar la calidad de la huella capturada
                    int qualityScore = FPutils.FPModule_GetQuality(finalTemplate);
                    if (qualityScore < 50) // Umbral de calidad
                    {
                        Console.WriteLine("Calidad insuficiente de la huella. Intente nuevamente.");
                        byte[] qualityBuffer = Encoding.UTF8.GetBytes("Error: Calidad insuficiente de la huella.");  // Usar 'qualityBuffer'
                        response.ContentLength64 = qualityBuffer.Length;
                        response.OutputStream.Write(qualityBuffer, 0, qualityBuffer.Length);
                        response.OutputStream.Close();
                        continue; // No continuar si la calidad es baja
                    }

                    Console.WriteLine($"Huella capturada con éxito. Calidad: {qualityScore}");

                    // Guardar la plantilla en un archivo con el ID del cliente
                    string filePath = $@"temp_{clienteId}.dat";
                    SaveTemp(filePath, finalTemplate);

                    // Responder con la confirmación
                    byte[] successBuffer = Encoding.UTF8.GetBytes($"{filePath}");  // Usar 'successBuffer'
                    response.ContentLength64 = successBuffer.Length;
                    response.OutputStream.Write(successBuffer, 0, successBuffer.Length);

                    // Enviar confirmación a Laravel (opcional)
                    NotificarLaravel(clienteId, filePath);
                }
                else
                {
                    // Si no se pudo capturar la huella correctamente
                    Console.WriteLine("Error al enrolar la huella.");
                    byte[] errorBuffer = Encoding.UTF8.GetBytes("Error: No se pudo enrolar la huella.");  // Reutilizar 'errorBuffer'
                    response.ContentLength64 = errorBuffer.Length;
                    response.OutputStream.Write(errorBuffer, 0, errorBuffer.Length);
                }

                response.OutputStream.Close();
            }
        }
    }

    // Guardar los datos de la huella en un archivo
    static void SaveTemp(string path, byte[] template)
    {
        FileStream file = new FileStream(path, FileMode.Create);
        file.Write(template, 0, template.Length);
        file.Close();
    }

    // Enviar confirmación a Laravel (opcional)
    static void NotificarLaravel(string clienteId, string filePath)
    {
        Console.WriteLine($"Confirmación enviada a Laravel para el cliente {clienteId} con la huella en {filePath}");
        // Aquí podrías usar HttpClient para enviar una solicitud POST a Laravel si es necesario
    }
} 




