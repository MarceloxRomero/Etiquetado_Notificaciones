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
    public class ProduccionDBContext : DbContext
    {

        public ProduccionDBContext(DbContextOptions<ProduccionDBContext> options) : base(options) { }
        public DbSet<EtiqProduccion> Produccion { get; set; }

    }

    [Table("PRODUCCION")]
    public class EtiqProduccion
    {
        [Key]
        [Column("UMA")]
        public string Uma { get; set; }

        [Column("LOTE")]
        public string Lote { get; set; }

        [Column("MATERIAL")]
        public string Material { get; set; }

        [Column("VERSIONF")]
        public string VersionF { get; set; }

        [Column("CENTRO")]
        public string Centro { get; set; }

        [Column("ALMACEN")]
        public string Almacen { get; set; }

        [Column("PALETIZADORA")]
        public int Paletizadora { get; set; }

        [Column("NORDPREV")]
        public string NOrdPrev { get; set; }

        [Column("FECHA")]
        public DateTime Fecha { get; set; }

        [Column("HORA")]
        public string Hora { get; set; }

        [Column("CANTIDAD")]
        public int Cantidad { get; set; }

        [Column("EXP_SAP")]
        public string ExpSAP { get; set; }

        [Column("N_DOC")]
        public string NDoc { get; set; }

        [Column("LI_MB")]
        public string LiMb { get; set; }

        [Column("LI_FQ")]
        public string LiFq { get; set; }

        [Column("CORRE_LINEA")]
        public int CorreLinea { get; set; }

        [Column("FECHA_SEMI")]
        public DateTime fechaSemi { get; set; }

        [Column("fechaCodificado")]
        public DateTime fechaCodificado { get; set; }
    }





}
