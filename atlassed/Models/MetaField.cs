﻿using Microsoft.SqlServer.Server;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace Atlassed.Models
{
    public enum MetaFieldType { STR, INT, DEC, BOOL, DATE, BRULE }

    public class MetaField : IDbRow<MetaField>
    {
        public const string _fieldName = "fieldName";
        private const string _fieldType = "fieldType";
        private const string _fieldLabel = "fieldLabel";
        private const string _fieldDescription = "fieldDescription";
        private const string _fieldIsRequired = "fieldIsRequired";
        private const string _defaultValue = "defaultValue";
        private const string _fieldIsUnique = "fieldIsUnique";
        private const string _metaConstraints = "metaConstraints";

        private const string _spAddMetaField = "AddMetaField";
        private const string _spEditMetaField = "EditMetaField";
        private const string _spDeleteMetaField = "DeleteMetaField";
        private const string _spGetMetaFields = "GetMetaFields";

        public string ClassName { get; private set; }
        public string FieldName { get; set; }
        public MetaFieldType FieldType { get; set; }
        public string FieldLabel { get; set; }
        public string FieldDescription { get; set; }
        public bool FieldIsRequired { get; set; }
        public bool FieldIsUnique { get; set; }
        public string MetaConstraints { get; set; }

        private readonly bool _isCommitted = false;

        public MetaField()
        {
            _isCommitted = false;
        }

        public MetaField(IDataRecord data)
        {
            ClassName = data.GetString(MetaClass._className);
            FieldName = data.GetString(_fieldName);
            FieldType = (MetaFieldType)Enum.Parse(typeof(MetaFieldType), data.GetString(_fieldType));
            FieldLabel = data.GetString(_fieldLabel);
            FieldDescription = data.GetString(_fieldDescription);
            FieldIsRequired = data.GetBoolean(_fieldIsRequired);
            FieldIsUnique = data.GetBoolean(_fieldIsUnique);
            MetaConstraints = data.GetString(_metaConstraints);

            _isCommitted = true;
        }

        public MetaField(string className, string fieldName, MetaFieldType fieldType, string fieldLabel, string fieldDescription, bool fieldIsRequired, string defaultValue, bool fieldIsUnique, string metaConstraints)
        {
            ClassName = className;
            FieldName = fieldName;
            FieldType = fieldType;
            FieldLabel = fieldLabel;
            FieldDescription = fieldDescription;
            FieldIsRequired = fieldIsRequired;
            FieldIsUnique = fieldIsUnique;
            MetaConstraints = metaConstraints;

            DB.NewSP(_spAddMetaField)
                .AddParam(MetaClass._className, ClassName)
                .AddParam(_fieldName, FieldName)
                .AddParam(_fieldType, FieldType.ToString())
                .AddParam(_fieldLabel, FieldLabel)
                .AddParam(_fieldDescription, FieldDescription)
                .AddParam(_fieldIsRequired, FieldIsRequired)
                .AddParam(_defaultValue, defaultValue)
                .AddParam(_fieldIsUnique, FieldIsUnique)
                .AddTVParam(_metaConstraints, GenerateMetaConstraintTable(MetaConstraints))
                .ExecuteNonQuery();

            _isCommitted = true;
        }

        public static IEnumerable<SqlDataRecord> GenerateMetaConstraintTable(string metaConstraints)
        {
            var metaConstraintsTable = new List<SqlDataRecord>();

            try
            {
                var reader = JObject.Parse(metaConstraints);
                SqlMetaData[] metaData = new SqlMetaData[2];
                metaData[0] = new SqlMetaData("ConstraintType", SqlDbType.VarChar, 10);
                metaData[1] = new SqlMetaData("ConstraintValue", SqlDbType.VarChar, 50);

                foreach (KeyValuePair<string, JToken> prop in reader)
                {
                    SqlDataRecord record = new SqlDataRecord(metaData);
                    record.SetString(0, prop.Key);
                    record.SetString(1, prop.Value.ToString());
                    metaConstraintsTable.Add(record);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

            return metaConstraintsTable;
        }

        public MetaField CommitUpdate()
        {
            if (!_isCommitted)
            {
                throw new Exception("record must be committed before updating");
            }

            return DB.NewSP(_spEditMetaField)
                    .AddParam(MetaClass._className, ClassName)
                    .AddParam(_fieldName, FieldName)
                    .AddParam(_fieldLabel, FieldLabel)
                    .AddParam(_fieldDescription, FieldDescription)
                    .AddParam(_fieldIsRequired, FieldIsRequired)
                    .AddParam(_fieldIsUnique, FieldIsUnique)
                    .AddTVParam(_metaConstraints, GenerateMetaConstraintTable(MetaConstraints))
                    .ExecNonQueryExpectSuccess()
                ? GetMetaField(ClassName, FieldName)
                : null;
        }

        public bool Delete()
        {
            if (!_isCommitted)
            {
                throw new Exception("record must be committed before deleting");
            }

            return DB.NewSP(_spDeleteMetaField)
                .AddParam(MetaClass._className, ClassName)
                .AddParam(_fieldName, FieldName)
                .ExecNonQueryExpectSuccess();
        }

        public static MetaField GetMetaField(string className, string fieldName)
        {

            return DB.NewSP(_spGetMetaFields)
                .AddParam(MetaClass._className, className)
                .AddParam(_fieldName, fieldName)
                .ExecExpectOne(x => new MetaField(x));
        }

        public static List<MetaField> GetAllMetaFields(string className)
        {
            return DB.NewSP(_spGetMetaFields)
                .AddParam(MetaClass._className, className)
                .ExecExpectMultiple(x => new MetaField(x)).ToList();
        }
    }
}