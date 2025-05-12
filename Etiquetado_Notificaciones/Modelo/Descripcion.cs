using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Etiquetado_Notificaciones.Modelo
{
    public class DescripcionDBContext : DbContext
    {
        public DescripcionDBContext(DbContextOptions<DescripcionDBContext> options) : base(options) { }
        public DbSet<Descripciones> Descripcion { get; set; }
    }

    [Table("DESCRIPCION")]
    public class Descripciones
    {
        [Key]
        [Column("Material")]
        public string Material { get; set; }

        [Column("Descripcion")]
        public string Descripcion { get; set; }

        [Column("LTXCJ")]
        public int LTxCJ { get; set; }

        [Column("UM")]
        public string Um { get; set; }
    }
}
