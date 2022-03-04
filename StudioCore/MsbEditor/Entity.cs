﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Numerics;
using System.Xml.Serialization;
using SoulsFormats;
using StudioCore.Scene;

namespace StudioCore.MsbEditor
{
    /// <summary>
    /// A logical map object that can be either a part, a region, or an event. Uses
    /// reflection to access and update properties
    /// </summary>
    public class Entity : Scene.ISelectable, IDisposable
    {
        public object WrappedObject { get; set; }

        private string CachedName = null;

        [XmlIgnore]
        public ObjectContainer Container { get; set; } = null;

        [XmlIgnore]
        public Universe Universe { 
            get
            {
                return (Container != null) ? Container.Universe : null;
            }
        }
        [XmlIgnore]
        public Entity Parent { get; private set; } = null;
        public List<Entity> Children { get; set; } = new List<Entity>();

        /// <summary>
        /// A map that contains references for each property
        /// </summary>
        [XmlIgnore]
        public Dictionary<string, Entity[]> References { get; private set; } = new Dictionary<string, Entity[]>();

        [XmlIgnore]
        public virtual bool HasTransform
        {
            get
            {
                return false;
            }
        }

        protected Scene.RenderableProxy _renderSceneMesh = null;
        [XmlIgnore]
        public Scene.RenderableProxy RenderSceneMesh
        {
            set
            {
                _renderSceneMesh = value;
                UpdateRenderModel();
            }
            get
            {
                return _renderSceneMesh;
            }
        }
        [XmlIgnore]
        public bool UseDrawGroups { set; get; } = false;

        [XmlIgnore]
        public virtual string Name
        {
            get
            {
                if (CachedName != null)
                {
                    return CachedName;
                }
                CachedName = (string)WrappedObject.GetType().GetProperty("Name").GetValue(WrappedObject, null);
                return CachedName;
            }
            set
            {
                if (value == null)
                {
                    CachedName = null;
                }
                else
                {
                    WrappedObject.GetType().GetProperty("Name").SetValue(WrappedObject, value);
                    CachedName = value;
                }
            }
        }

        [XmlIgnore]
        public virtual string PrettyName
        {
            get
            {
                return Name;
            }
        }

        protected string CurrentModel = "";

        [XmlIgnore]
        public uint[] Drawgroups
        {
            get
            {
                var prop = WrappedObject.GetType().GetProperty("DrawGroups");
                if (prop != null)
                {
                    return (uint[])prop.GetValue(WrappedObject);
                }
                return null;
            }
        }

        [XmlIgnore]
        public uint[] Dispgroups
        {
            get
            {
                var prop = WrappedObject.GetType().GetProperty("DispGroups");
                if (prop != null)
                {
                    return (uint[])prop.GetValue(WrappedObject);
                }
                return null;
            }
        }

        protected bool _EditorVisible = true;
        [XmlIgnore]
        public bool EditorVisible
        {
            get
            {
                return _EditorVisible;
            }
            set
            {
                _EditorVisible = value;
                if (RenderSceneMesh != null)
                {
                    RenderSceneMesh.Visible = _EditorVisible;
                }
            }
        }

        internal bool UseTempTransform = false;
        internal Transform TempTransform = Transform.Default;

        public Entity()
        {

        }

        public Entity(ObjectContainer map, object msbo)
        {
            Container = map;
            WrappedObject = msbo;
        }

        public void AddChild(Entity child)
        {
            if (child.Parent != null)
            {
                Parent.Children.Remove(child);
            }
            child.Parent = this;
            Children.Add(child);
            child.UpdateRenderModel();
        }

        public void AddChild(Entity child, int index)
        {
            if (child.Parent != null)
            {
                Parent.Children.Remove(child);
            }
            child.Parent = this;
            Children.Insert(index, child);
            child.UpdateRenderModel();
        }

        public int ChildIndex(Entity child)
        {
            for (int i = 0; i < Children.Count(); i++)
            {
                if (Children[i] == child)
                {

                    return i;
                }
            }
            return -1;
        }

        public int RemoveChild(Entity child)
        {
            for (int i = 0; i < Children.Count(); i++)
            {
                if (Children[i] == child)
                {
                    Children[i].Parent = null;
                    Children.RemoveAt(i);
                    return i;
                }
            }
            return -1;
        }

        private void CloneRenderable(Entity obj)
        {
            if (RenderSceneMesh != null)
            {
                if (RenderSceneMesh is MeshRenderableProxy m)
                {
                    obj.RenderSceneMesh = new MeshRenderableProxy(m);
                    obj.RenderSceneMesh.SetSelectable(this);
                }
                else if (RenderSceneMesh is DebugPrimitiveRenderableProxy c)
                {
                    obj.RenderSceneMesh = new DebugPrimitiveRenderableProxy(c);
                    obj.RenderSceneMesh.SetSelectable(this);
                }
            }
        }

        internal virtual Entity DuplicateEntity(object clone)
        {
            return new Entity(Container, clone);
        }

        private object DeepCopyObject(object obj)
        {
            var typ = obj.GetType();

            // use copy constructor if available
            var typs = new Type[1];
            typs[0] = typ;
            ConstructorInfo constructor = typ.GetConstructor(typs);
            if (constructor != null)
            {
                return constructor.Invoke(new object[] { obj });
            }
            else
            {
                // Try either default constructor or name constructor
                typs[0] = typeof(string);
                constructor = typ.GetConstructor(typs);
                object clone;
                if (constructor != null)
                {
                    clone = constructor.Invoke(new object[] { "" });
                }
                else
                {
                    // Otherwise use standard constructor and abuse reflection
                    constructor = typ.GetConstructor(System.Type.EmptyTypes);
                    clone = constructor.Invoke(null);
                }
                foreach (PropertyInfo sourceProperty in typ.GetProperties())
                {
                    PropertyInfo targetProperty = typ.GetProperty(sourceProperty.Name);
                    if (sourceProperty.PropertyType.IsArray)
                    {
                        Array arr = (Array)sourceProperty.GetValue(obj);
                        Array.Copy(arr, (Array)targetProperty.GetValue(clone), arr.Length);
                    }
                    else if (sourceProperty.CanWrite)
                    {
                        if (sourceProperty.PropertyType.IsClass && sourceProperty.PropertyType != typeof(string))
                        {
                            targetProperty.SetValue(clone, DeepCopyObject(sourceProperty.GetValue(obj, null)), null);
                        }
                        else
                        {
                            targetProperty.SetValue(clone, sourceProperty.GetValue(obj, null), null);
                        }
                    }
                    else
                    {
                        // Sanity check
                        // Console.WriteLine($"Can't copy {type.Name} {sourceProperty.Name} of type {sourceProperty.PropertyType}");
                    }
                }
                return clone;
            }
        }

        public virtual Entity Clone()
        {
            var clone = DeepCopyObject(WrappedObject);
            var obj = DuplicateEntity(clone);
            CloneRenderable(obj);
            obj.UseDrawGroups = UseDrawGroups;
            return obj;
        }

        public object GetPropertyValue(string prop)
        {
            if (WrappedObject == null)
            {
                return null;
            }
            if (WrappedObject is PARAM.Row row)
            {
                var pp = row.Cells.FirstOrDefault(cell => cell.Def.InternalName == prop);
                if (pp != null)
                {
                    return pp.Value;
                }
            }
            else if (WrappedObject is MergedParamRow mrow)
            {
                var pp = mrow[prop];
                if (pp != null)
                {
                    return pp.Value;
                }
            }
            var p = WrappedObject.GetType().GetProperty(prop);
            if (p != null)
            {
                return p.GetValue(WrappedObject, null);
            }
            return null;
        }

        public bool IsRotationPropertyRadians(string prop)
        {
            if (WrappedObject == null)
            {
                return false;
            }
            if (WrappedObject is PARAM.Row row || WrappedObject is MergedParamRow mrow)
            {
                return false;
            }
            return WrappedObject.GetType().GetProperty(prop).GetCustomAttribute<RotationRadians>() != null;
        }

        public T GetPropertyValue<T>(string prop)
        {
            if (WrappedObject == null)
            {
                return default(T);
            }
            if (WrappedObject is PARAM.Row row)
            {
                var pp = row.Cells.FirstOrDefault(cell => cell.Def.InternalName == prop);
                if (pp != null)
                {
                    return (T)pp.Value;
                }
            }
            else if (WrappedObject is MergedParamRow mrow)
            {
                var pp = mrow[prop];
                if (pp != null)
                {
                    return (T)pp.Value;
                }
            }
            var p = WrappedObject.GetType().GetProperty(prop);
            if (p != null && p.PropertyType == typeof(T))
            {
                return (T)p.GetValue(WrappedObject, null);
            }
            return default(T);
        }

        public PropertyInfo GetProperty(string prop)
        {
            if (WrappedObject == null)
            {
                return null;
            }
            if (WrappedObject is PARAM.Row row)
            {
                var pp = row[prop];
                if (pp != null)
                {
                    return pp.GetType().GetProperty("Value");
                }
            }
            else if (WrappedObject is MergedParamRow mrow)
            {
                var pp = mrow[prop];
                if (pp != null)
                {
                    return pp.GetType().GetProperty("Value");
                }
            }
            var p = WrappedObject.GetType().GetProperty(prop);
            if (p != null)
            {
                return p;
            }
            return null;
        }

        public PropertiesChangedAction GetPropertyChangeAction(string prop, object newval)
        {
            if (WrappedObject == null)
            {
                return null;
            }
            if (WrappedObject is PARAM.Row row)
            {
                var pp = row[prop];
                if (pp != null)
                {
                    var pprop = pp.GetType().GetProperty("Value");
                    return new PropertiesChangedAction(pprop, pp, newval);
                }
            }
            if (WrappedObject is MergedParamRow mrow)
            {
                var pp = mrow[prop];
                if (pp != null)
                {
                    var pprop = pp.GetType().GetProperty("Value");
                    return new PropertiesChangedAction(pprop, pp, newval);
                }
            }
            var p = WrappedObject.GetType().GetProperty(prop);
            if (p != null)
            {
                return new PropertiesChangedAction(p, WrappedObject, newval);
            }
            return null;
        }

        public void BuildReferenceMap()
        {
            if (!(WrappedObject is PARAM.Row) && !(WrappedObject is MergedParamRow))
            {
                var type = WrappedObject.GetType();
                var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                foreach (var p in props)
                {
                    var att = p.GetCustomAttribute<MSBReference>();
                    if (att != null)
                    {
                        if (p.PropertyType.IsArray)
                        {

                        }
                        else
                        {
                            var sref = (string)p.GetValue(WrappedObject);
                            if (sref != null && sref != "")
                            {
                                var obj = Container.GetObjectByName(sref);
                                if (obj != null)
                                {
                                    References.Add(p.Name, new[] { obj });
                                }
                            }
                        }
                    }
                }
            }
        }

        private HashSet<Entity> ReferencingObjects = null;
        private bool disposedValue;

        public IReadOnlyCollection<Entity> GetReferencingObjects()
        {
            if (Container == null)
            {
                return new List<Entity>();
            }

            if (ReferencingObjects != null)
            {
                return ReferencingObjects;
            }

            ReferencingObjects = new HashSet<Entity>();
            foreach (var m in Container.Objects)
            {
                if (m.References != null)
                {
                    foreach (var n in m.References)
                    {
                        foreach (var o in n.Value)
                        {
                            if (o == this)
                            {
                                ReferencingObjects.Add(m);
                            }
                        }
                    }
                }
            }

            return ReferencingObjects;
        }

        public void InvalidateReferencingObjectsCache()
        {
            ReferencingObjects = null;
        }

        public virtual Transform GetLocalTransform()
        {
            var t = Transform.Default;
            var pos = GetPropertyValue("Position");
            if (pos != null)
            {
                t.Position = (Vector3)pos;
            }
            else
            {
                var px = GetPropertyValue("PositionX");
                var py = GetPropertyValue("PositionY");
                var pz = GetPropertyValue("PositionZ");
                if (px != null)
                {
                    t.Position.X = (float)px;
                }
                if (py != null)
                {
                    t.Position.Y = (float)py;
                }
                if (pz != null)
                {
                    t.Position.Z = (float)pz;
                }
            }

            var rot = GetPropertyValue("Rotation");
            if (rot != null)
            {
                var r = (Vector3)rot;
                if (IsRotationPropertyRadians("Rotation"))
                {
                    t.EulerRotation = new Vector3(r.X, r.Y, r.Z);
                }
                else
                {
                    t.EulerRotation = new Vector3(Utils.DegToRadians(r.X), Utils.DegToRadians(r.Y), Utils.DegToRadians(r.Z));
                }
            }
            else
            {
                var rx = GetPropertyValue("RotationX");
                var ry = GetPropertyValue("RotationY");
                var rz = GetPropertyValue("RotationZ");
                Vector3 r = Vector3.Zero;
                if (rx != null)
                {
                    r.X = (float)rx;
                }
                if (ry != null)
                {
                    r.Y = (float)ry + 180.0f; // According to Vawser, DS2 enemies are flipped 180 relative to map rotations
                }
                if (rz != null)
                {
                    r.Z = (float)rz;
                }
                t.EulerRotation = new Vector3(Utils.DegToRadians(r.X), Utils.DegToRadians(r.Y), Utils.DegToRadians(r.Z));
            }

            var scale = GetPropertyValue("Scale");
            if (scale != null)
            {
                t.Scale = (Vector3)scale;
            }

            return t;
        }

        public virtual Matrix4x4 GetWorldMatrix()
        {
            Matrix4x4 t = UseTempTransform ? TempTransform.WorldMatrix : GetLocalTransform().WorldMatrix;
            var p = Parent;
            while (p != null)
            {
                if (p.HasTransform)
                {
                    t = t * (p.UseTempTransform ? p.TempTransform.WorldMatrix : p.GetLocalTransform().WorldMatrix);
                }
                p = p.Parent;
            }
            return t;
        }

        public void SetTemporaryTransform(Transform t)
        {
            TempTransform = t;
            UseTempTransform = true;
            UpdateRenderModel();
        }

        public void ClearTemporaryTransform(bool updaterender=true)
        {
            UseTempTransform = false;
            if (updaterender)
            {
                UpdateRenderModel();
            }
        }

        public Action GetUpdateTransformAction(Transform newt)
        {
            if (WrappedObject is PARAM.Row || WrappedObject is MergedParamRow)
            {
                var actions = new List<Action>();
                float roty = newt.EulerRotation.Y * Utils.Rad2Deg - 180.0f;
                actions.Add(GetPropertyChangeAction("PositionX", newt.Position.X));
                actions.Add(GetPropertyChangeAction("PositionY", newt.Position.Y));
                actions.Add(GetPropertyChangeAction("PositionZ", newt.Position.Z));
                actions.Add(GetPropertyChangeAction("RotationX", newt.EulerRotation.X * Utils.Rad2Deg));
                actions.Add(GetPropertyChangeAction("RotationY", roty));
                actions.Add(GetPropertyChangeAction("RotationZ", newt.EulerRotation.Z * Utils.Rad2Deg));
                var act = new CompoundAction(actions);
                act.SetPostExecutionAction((undo) =>
                {
                    UpdateRenderModel();
                });
                return act;
            }
            else
            {
                var act = new PropertiesChangedAction(WrappedObject);
                var prop = WrappedObject.GetType().GetProperty("Position");
                act.AddPropertyChange(prop, newt.Position);
                prop = WrappedObject.GetType().GetProperty("Rotation");
                if (prop != null)
                {
                    act.AddPropertyChange(prop, newt.EulerRotation * Utils.Rad2Deg);
                }
                act.SetPostExecutionAction((undo) =>
                {
                    UpdateRenderModel();
                });
                return act;
            }
        }

        public virtual void UpdateRenderModel()
        {
            if (!HasTransform)
            {
                return;
            }
            Matrix4x4 t = UseTempTransform ? TempTransform.WorldMatrix : GetLocalTransform().WorldMatrix;
            var p = Parent;
            while (p != null)
            {
                t = t * (p.UseTempTransform ? p.TempTransform.WorldMatrix : p.GetLocalTransform().WorldMatrix);
                p = p.Parent;
            }
            if (RenderSceneMesh != null)
            {
                RenderSceneMesh.World = t;
            }
            foreach (var c in Children)
            {
                if (c.HasTransform)
                {
                    c.UpdateRenderModel();
                }
            }

            if (UseDrawGroups)
            {
                var prop = WrappedObject.GetType().GetProperty("DrawGroups");
                if (prop != null && RenderSceneMesh != null)
                {
                    RenderSceneMesh.DrawGroups.AlwaysVisible = false;
                    RenderSceneMesh.DrawGroups.Drawgroups = (uint[])prop.GetValue(WrappedObject);
                }
            }

            if (RenderSceneMesh != null)
            {
                RenderSceneMesh.Visible = _EditorVisible;
            }
        }

        public void OnSelected()
        {
            if (RenderSceneMesh != null)
            {
                RenderSceneMesh.RenderSelectionOutline = true;
            }
        }

        public void OnDeselected()
        {
            if (RenderSceneMesh != null)
            {
                RenderSceneMesh.RenderSelectionOutline = false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (RenderSceneMesh != null)
                    {
                        RenderSceneMesh.Dispose();
                        _renderSceneMesh = null;
                    }
                }

                disposedValue = true;
            }
        }

        ~Entity()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class NamedEntity : Entity
    {
        public override string Name { get; set; }

        public NamedEntity(ObjectContainer map, object msbo, string name) : base(map, msbo)
        {
            Name = name;
        }
    }

    public class TransformableNamedEntity : Entity
    {
        public override string Name { get; set; }

        public TransformableNamedEntity(ObjectContainer map, object msbo, string name) : base(map, msbo)
        {
            Name = name;
        }

        public override bool HasTransform
        {
            get
            {
                return true;
            }
        }
    }

    public class MapSerializationEntity
    {
        public string Name { get; set; } = null;
        public int Msbidx { get; set; } = -1;
        public MapEntity.MapEntityType Type { get; set; }
        public Transform Transform { get; set; }
        public List<MapSerializationEntity> Children { get; set; } = null;

        public bool ShouldSerializeChildren()
        {
            return (Children != null && Children.Count > 0);
        }
    }

    public class MapEntity : Entity
    {
        public enum MapEntityType
        {
            MapRoot,
            Editor,
            Part,
            Region,
            Event,
            DS2Generator,
            DS2GeneratorRegist,
            DS2Event,
            DS2EventLocation,
            DS2ObjectInstance,
        }

        public MapEntityType Type { get; set; }

        public Map ContainingMap
        {
            get
            {
                return (Map)Container;
            }
        }

        public override string PrettyName
        {
            get
            {
                string icon = "";
                if (Type == MapEntityType.Part)
                {
                    icon = ForkAwesome.PuzzlePiece;
                }
                else if (Type == MapEntityType.Event)
                {
                    icon = ForkAwesome.Flag;
                }
                else if (Type == MapEntityType.Region)
                {
                    icon = ForkAwesome.LocationArrow;
                }
                else if (Type == MapEntityType.DS2Generator)
                {
                    icon = ForkAwesome.Male;
                }
                else if (Type == MapEntityType.DS2GeneratorRegist)
                {
                    icon = ForkAwesome.UserCircleO;
                }
                else if (Type == MapEntityType.DS2EventLocation)
                {
                    icon = ForkAwesome.FlagO;
                }
                else if (Type == MapEntityType.DS2Event)
                {
                    icon = ForkAwesome.FlagCheckered;
                }
                else if (Type == MapEntityType.DS2ObjectInstance)
                {
                    icon = ForkAwesome.Database;
                }

                return $@"{icon} {Name}";
            }
        }

        public override bool HasTransform
        {
            get
            {
                return Type != MapEntityType.Event && Type != MapEntityType.DS2GeneratorRegist && Type != MapEntityType.DS2Event;
            }
        }

        [XmlIgnore]
        public string MapID
        {
            get
            {
                var parent = Parent;
                while (parent != null && parent is MapEntity e)
                {
                    if (e.Type == MapEntityType.MapRoot)
                    {
                        return parent.Name;
                    }
                    parent = parent.Parent;
                }
                return null;
            }
        }

        public MapEntity()
        {

        }

        public MapEntity(ObjectContainer map, object msbo)
        {
            Container = map;
            WrappedObject = msbo;
        }

        public MapEntity(ObjectContainer map, object msbo, MapEntityType type)
        {
            Container = map;
            WrappedObject = msbo;
            Type = type;
            if (!(msbo is PARAM.Row) && !(msbo is MergedParamRow))
            {
                CurrentModel = GetPropertyValue<string>("ModelName");
            }
        }

        public override void UpdateRenderModel()
        {
            // If the model field changed, then update the visible model
            if (Type == MapEntityType.DS2Generator)
            {

            }
            else if (Type == MapEntityType.DS2EventLocation && _renderSceneMesh == null)
            {
                if (_renderSceneMesh != null)
                {
                    _renderSceneMesh.Dispose();
                }
                _renderSceneMesh = Universe.GetDS2EventLocationDrawable(ContainingMap, this);
            }
            else if (Type == MapEntityType.Region && _renderSceneMesh == null)
            {
                if (_renderSceneMesh != null)
                {
                    _renderSceneMesh.Dispose();
                }
                _renderSceneMesh = Universe.GetRegionDrawable(ContainingMap, this);
            }
            else
            {
                var model = GetPropertyValue<string>("ModelName");
                if (model != null && model != CurrentModel)
                {
                    _renderSceneMesh.Dispose();
                    CurrentModel = model;
                    _renderSceneMesh = Universe.GetModelDrawable(ContainingMap, this, model, true);
                    if (Universe.Selection.IsSelected(this))
                    {
                        OnSelected();
                    }
                }
            }

            base.UpdateRenderModel();
        }

        public override Transform GetLocalTransform()
        {
            var t = base.GetLocalTransform();
            // If this is a region scale the region primitive by its respective parameters
            if (Type == MapEntityType.Region)
            {
                var shape = GetPropertyValue("Shape");
                if (shape != null && shape is MSB.Shape.Box b2)
                {
                    t.Scale = new Vector3(b2.Width, b2.Height, b2.Depth);
                }
                else if (shape != null && shape is MSB.Shape.Sphere s)
                {
                    t.Scale = new Vector3(s.Radius);
                }
                else if (shape != null && shape is MSB.Shape.Cylinder c)
                {
                    t.Scale = new Vector3(c.Radius, c.Height, c.Radius);
                }
            }

            // DS2 event regions
            if (Type == MapEntityType.DS2EventLocation)
            {
                var sx = GetPropertyValue("ScaleX");
                var sy = GetPropertyValue("ScaleY");
                var sz = GetPropertyValue("ScaleZ");
                if (sx != null)
                {
                    t.Scale.X = (float)sx;
                }
                if (sy != null)
                {
                    t.Scale.Y = (float)sy;
                }
                if (sz != null)
                {
                    t.Scale.Z = (float)sz;
                }
            }
            return t;
        }

        internal override Entity DuplicateEntity(object clone)
        {
            return new MapEntity(Container, clone);
        }

        public override Entity Clone()
        {
            MapEntity c = (MapEntity)base.Clone();
            c.Type = Type;
            return c;
        }

        public MapSerializationEntity Serialize(Dictionary<Entity, int> idmap)
        {
            var e = new MapSerializationEntity();
            e.Name = Name;
            if (HasTransform)
            {
                e.Transform = GetLocalTransform();
            }
            e.Type = Type;
            e.Children = new List<MapSerializationEntity>();
            if (idmap.ContainsKey(this))
            {
                e.Msbidx = idmap[this];
            }
            foreach (var c in Children)
            {
                e.Children.Add(((MapEntity)c).Serialize(idmap));
            }
            return e;
        }
    }
}
