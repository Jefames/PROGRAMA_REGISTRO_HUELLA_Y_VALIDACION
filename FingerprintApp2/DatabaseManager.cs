using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

public class DatabaseManager
{
    private string connectionString;

    public DatabaseManager()
    {
        // Crear la cadena de conexión con los parámetros que has proporcionado
        connectionString = $"Server=127.0.0.1;Port=3306;Database=gym;User ID=root;Password=;";
    }

    // Método para obtener los IDs de clientes que tienen huellas almacenadas
    public List<int> ObtenerClientesConHuella()
    {
        List<int> clientesConHuella = new List<int>();

        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            connection.Open();

            string query = "SELECT id FROM clientes WHERE biometrico IS NOT NULL"; // Solo clientes con huellas almacenadas

            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        clientesConHuella.Add(reader.GetInt32("id"));
                    }
                }
            }
        }

        return clientesConHuella;
    }

    // Método para verificar si el cliente tiene una suscripción activa
    public bool TieneSuscripcionActiva(int clienteId)
    {
        using (MySqlConnection connection = new MySqlConnection(connectionString))
        {
            connection.Open();

            // Consulta para verificar si el cliente tiene una suscripción activa
            string query = @"
                SELECT COUNT(*) 
                FROM cliente_membresia 
                WHERE cliente_id = @clienteId 
                AND estado = 'activa' 
                AND CURDATE() BETWEEN fecha_inicio AND fecha_finalizacion";

            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@clienteId", clienteId);

                // Ejecutar la consulta y verificar si hay una suscripción activa
                int count = Convert.ToInt32(command.ExecuteScalar());

                return count > 0;
            }
        }
    }
}




