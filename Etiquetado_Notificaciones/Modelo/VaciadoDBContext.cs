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
    public class VaciadoDBContext : DbContext
    {
        public VaciadoDBContext(DbContextOptions<VaciadoDBContext> options) : base(options) { }
        public DbSet<Vaciado> Produccion { get; set; }

    }

    [Table("VACIADO")]
    public class Vaciado
    {
        [Key]
        [Column("UMA")]
        public string Uma { get; set; }

        [Column("CENTRO")]
        public string Centro { get; set; }
        
        [Column("ALMACEN")]
        public string Almacen { get; set; }
        
        [Column("FECHA")]
        public DateTime Fecha { get; set; }
        
        [Column("PALETIZADORA")]
        public string Paletizadora { get; set; }
        
        [Column("HORA")]
        public string Hora { get; set; }
        
        [Column("Version")]
        public string Version { get; set; }
        
        [Column("ORDEN_PREV")]
        public string OrdenPrev { get; set; }
        
        [Column("CANTIDAD")]
        public int Cantidad { get; set; }
        
        [Column("MATERIAL_ORDEN")]
        public string MaterialOrden { get; set; }
        
        [Column("LOTE_CONSUMO")]
        public string LoteConsumo { get; set; }
        
        [Column("N_DOC_ASIG")]
        public string NDocAsig { get; set; }
        
        [Column("N_DOC_TRASP")]
        public string NDocTrasp { get; set; }
        
        [Column("N_DOC_DES")]
        public string NDocDes { get; set; }
        
        [Column("DESEMBALA")]
        public string Desembala { get; set; }

    }
}
