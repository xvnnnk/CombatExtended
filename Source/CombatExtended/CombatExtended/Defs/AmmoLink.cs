﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class ChargeUser
    {
        //Same format as explosive comp fragments -- ALTHOUGH THINGDEFCOUNT IS ENOUGH?
        public List<ThingDefCountClass> projectiles;
        public int chargesUsed;

        //Normally:
        //<chargesUsed>chargesUsed</chargesUsed>
        //<projectiles>
        //  <Projectile>amountFired</Projectile>
        //</projectiles>
        //Add fallback for <Projectile Used="chargesUsed">amountFired</Projectile>?
        //Add fallback for <Projectile Used="chargesUsed" Fired="amountFired"/>?
    }

    public class AmmoLink
    {
        /*Normally:
        <ammoTypes>
            <li>
                <adders>
                    <li>
                        <Ammo>X</Ammo>
                        <isFallback>True</isFallback>
                    </li>
                    <li>
                        <Ammo>X</Ammo>
                    </li>
                </adders>
                <users>
                    <li>
                        <chargesUsed>Y</chargesUsed>
                        <projectiles>
                            <Projectile>A</Projectile>
                            <Projectile>A</Projectile>
                        </projectiles>
                    </li>
                    <li>
                        <chargesUsed>1</chargesUsed>
                        <projectiles>
                            <Projectile>A</Projectile>
                        </projectiles>
                </users>
                <spentThingDef>SpentAmmo</spentThingDef>
                <fallbackThingDef>Fallback</fallbackThingDef>
                <autogenerateFallbackUser>True</autogenerateFallbackUser>
                <chanceToRecoverBacklog>True</chanceToRecoverBacklog>
            </li>
        </ammoTypes>
        */
        //Fallback for <Ammo Added="chargesAdded" Used="chargesUsed">Projectile</Ammo>?

        /*(non-int X/Y CASE)
        //If nothing:
        //- Fire shot as long as at least one charge remains
        //With fallback user with appropriate X / Y integer:
        //- Fallback shot is fired
        //With boolean (bool autogenerateFallbackUser == true):
        //- Fallback is autogenerated with lower pelletCount and other stats
        */

        /*(non-int Y/X CASE)
        //If nothing:
        //- Unload backlog charges by destroying them
        //With spent ammo (ChargeAdder.spentThingDef)
        //- Unload spent ammo, any backlog is turned to spent ammo
        //With chance to return (bool chanceToRecoverBacklog == true)
        //- Recover full cartridge based on discrepancy
        //With fallback (ChargeAdder.fallbackThingDef)
        //- Unload fallback thing for every backlog charge
        */

        /*(Magazine size issues)
        //With variable 
        //- Calculate magazine size from X, Y (both lead and set limits somehow)
        */

        #region Fields
        //Index of lists is the amount of charges added or used?
        public List<ThingDefCountClass> adders;
        public List<ChargeUser> users;

        /// <summary>Whether there is a adder.ammo.count / CurMagCount chance (0f to 1f) chance to recover the smallest available adder on backlog</summary>
        public bool chanceToRecoverBacklog = true;
        /// <summary>Whether a many-charge ammo thingDef can be unloaded in-full, displaying an underflow of charge in the weapon</summary>
        public bool allowUnderflow = false;
        /// <summary>Whether a many-charge ammo thingDef can be loaded in-full, displaying an overflow of charge in the weapon</summary>
        public bool allowOverflow = false;
        /// <summary>Whether overflow wastes charges</summary>
        public bool wastefulOverflow = false;
        /// <summary>Whether the index of the charge adder and that of the charge user it creates are the same - useful for e.g catapults</summary>
        public bool directAdderUserLinkage = false;
        
        public ThingDef iconAdder;
        public AmmoCategoryDef ammoClass;
        public int defaultAmmoCount = -1;
        public string labelCap;
        public string labelCapShort;
        #endregion

      //AmmoDef _ammo;
      //ThingDef _projectile;
      
        #region Methods
        /*
        public AmmoLink() { }

        public AmmoLink(AmmoDef ammo, ThingDef projectile)
        {
            this.ammo = ammo;
            this.projectile = projectile;
        }*/

        public virtual IEnumerable<ThingDef> AllAdders()
        {
            yield return iconAdder;
        }

        public virtual bool CanAdd(ThingDef def)
        {
            return def == iconAdder;
        }

        /// <summary>Can the item be added to this ammoLink, and how many charges does it add</summary>
        /// <param name="def"></param>
        /// <param name="adder">ThingDef and amount of charges added by that def</param>
        /// <returns></returns>
        public virtual bool CanAdd(ThingDef def, out int chargesPerUnit)
        {
            chargesPerUnit = CanAdd(def) ? adders.First().count : -1;
            return chargesPerUnit != -1;
        }

        public virtual int AmountForDeficit(ThingDef def, int chargeDeficit, bool forLoading = false)
        {
            if (CanAdd(def, out int chargesPerUnit))
            {
                //Consider maximum loaded and maximum provided
                //3. Allow an overflow
                return (forLoading || allowOverflow || wastefulOverflow)
                    ? Mathf.CeilToInt(chargeDeficit / (float)chargesPerUnit)
                    : Mathf.FloorToInt(chargeDeficit / (float)chargesPerUnit);
            }

            return 0;
        }

        public virtual int AmountForDeficit(Thing thing, int chargeDeficit, bool forLoading = false)
        {
            var val = AmountForDeficit(thing.def, chargeDeficit, forLoading);
            if (val > 0)
                val = Math.Min(thing.stackCount, val);

            return val;
        }

        public virtual int AmountToConsume(Thing thing, CompAmmoUser user, bool ignoreLoadedCharge = false)
        {
            var magSize = Math.Max(1, user.Props.magazineSize);
            if (thing != null && user.CurMagCount < magSize)
            {
                return AmountForDeficit(thing, (!ignoreLoadedCharge || user.CurMagCount < 0)
                    ? magSize - user.CurMagCount
                    : magSize);
            }

            return 0;
        }

        /*
        public virtual IEnumerable<Thing> BestFullMagazine(List<Thing> things, CompAmmoUser user)
        {
            foreach (var adder in things
                .Select(x => { CanAdd(x, user, out var adder); return adder; })
                .OrderByDescending(y => y.ammo.count))
            {

            }
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="things">A collection of Things, e.g from the inventory</param>
        /// <param name="user"></param>
        /// <param name="chargeCount">The amount of charges added per instance of adder consumed</param>
        /// <param name="defCount">The amount of charges associated with the returned Thing</param>
        /// <returns></returns>
        public virtual Thing BestAdder(IEnumerable<Thing> things, CompAmmoUser user, out int chargeCount, bool maxStackSize = false)
        {
            var adder = adders.FirstOrDefault();
            if (adder != null)
            {
                var thing = things.FirstOrDefault(x => x.def == adder.thingDef);

                if (thing != null)
                {
                    chargeCount = adder.count;
                    return thing;
                }
            }
            chargeCount = 0;
            return null;
        }
        
        public virtual ChargeUser BestUser(CompAmmoUser user)
        {
            return (!user.HasMagazine || user.CurMagCount > 0)
                ? users.First()
                : null;
        }

        /*
        /// <summary>
        /// Reload the gun once - e.g, load with as many, as large as possible pieces of ammo
        /// </summary>
        /// <param name="defCount">Amount of thingDef used during reload</param>
        /// <returns>Charges loaded with this reload</returns>
        public virtual int ReloadNext(List<Thing> things, CompAmmoUser user, out ThingDefCount defCount, bool maxStackSize = false)
        {
            var thing = BestAdder(things, user, out var chargePerThing, maxStackSize);
            var chargesAdded = LoadThing(thing, user, out int amountUsed);
            defCount = new ThingDefCount(thing.def, amountUsed);
            return chargesAdded;
        }*/

        public virtual int LoadThing(Thing thing, CompAmmoUser user, out int amountUsed)
        {
            amountUsed = AmountToConsume(thing, user);

            if (amountUsed > 0)
            {
                if (user.Props.reloadOneAtATime || !user.HasMagazine)
                    amountUsed = 1;

                var chargesAdded = amountUsed * adders.Find(x => x.thingDef == thing.def).count;
                var magSize = Math.Max(1, user.Props.magazineSize);

                //1. Allow a wasteful overflow
                if (wastefulOverflow && chargesAdded + user.CurMagCount > magSize)
                    chargesAdded = magSize;

                return chargesAdded;
            }
            
            return 0;
        }
        
        public virtual Thing UnloadAdder(Thing thing, CompAmmoUser user, out int deficit, ref bool isSpent)
        {
            deficit = user.currentAdderCharge;

            if (thing.stackCount <= 0 || user.DiscardRounds)
                return null;

            if (!isSpent)
            {
                //For some reason isn't loadable.. just used to get charges per unit (cpu)
                if (!CanAdd(thing.def, out int cpu))
                    return null;

                //4. Create a fallback ThingDef for X = 1 or appropriate value, which backlog charges are converted to
                //TODO?

                //3. Give a chance to recover a full X cartridge depending on the discrepancy between X and charge
                //5. If underflow is allowed, use that
                if (deficit == cpu || allowUnderflow || (chanceToRecoverBacklog && Rand.Value < deficit / cpu))
                {
                    if (allowUnderflow && user.CurMagCount - deficit == 0)
                        deficit = cpu;

                    return thing;
                }
            }

            isSpent = true;

            // [XX 2. XX] NOT USED Convert non-X charge count to fallback ThingDefs - no partially spent rounds allowed

            if (IsSpentAdder(thing.def))
                return thing;
            else
            {
                var spentThing = ThingMaker.MakeThing((user.CurrentAdder.def as AmmoDef)?.spentThingDef) ?? null;
                if (spentThing != null)
                    spentThing.stackCount = thing.stackCount;
                return spentThing;
            }
        }

        public virtual bool CountFirstAdder(CompAmmoUser user)
        {
            return user.currentAdderCharge == adders.First(x => x.thingDef == user.CurrentAdder.def).count;
        }

        /// <summary>
        /// Whether the current def can be ignored for thingdef spawning reasons
        /// </summary>
        /// <param name="def"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public virtual bool IsSpentAdder(ThingDef def)
        {
            return (CanAdd(def)
                && (def as AmmoDef).spentThingDef == null
                && (def as AmmoDef).conservedMassFactorWhenFired > 0f);
        }

        public virtual int UnloadNext(CompAmmoUser user, out ThingDefCount defCount)
        {
            if (user.CurMagCount > 0)
            {
                var availableAdders = adders.Where(x => x.count <= user.CurMagCount);

                if (availableAdders != null)
                {
                    var most = availableAdders.MaxBy(x => x.count);
                    var addersRemoved = (allowUnderflow && most.count == adders.MinBy(x => x.count).count)
                        ? Mathf.CeilToInt((float)user.CurMagCount / (float)most.count)
                        : Mathf.FloorToInt((float)user.CurMagCount / (float)most.count);

                    defCount = new ThingDefCount(most.thingDef, addersRemoved);
                    return addersRemoved * most.count;
                }
            }
            else
                Log.Error("AmmoLink.UnloadNext called with CurMagCount == 0");

            //1. Unload charge counts below X by destroying them
            defCount = null;
            return user.CurMagCount;
        }
        #endregion

        #region XML-related
        ThingDef _ammo;
        ThingDef _projectile;
        int _ammoCharge = 1;
        int _projCharge = 1;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            if (xmlRoot.ChildNodes.Count == 1)
            {
                DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, GetType().GetField("_ammo", BindingFlags.NonPublic | BindingFlags.Instance), xmlRoot.Name);
                DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, GetType().GetField("_projectile", BindingFlags.NonPublic | BindingFlags.Instance), (string)ParseHelper.FromString(xmlRoot.FirstChild.Value, typeof(string)));

                //DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, this.GetType().GetField("adders"), xmlRoot.Name);
                //DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, this.GetType().GetField("users"), (string)ParseHelper.FromString(xmlRoot.FirstChild.Value, typeof(string)));

                if (xmlRoot.Attributes["AmmoCharge"] != null)
                    _ammoCharge = (int)ParseHelper.FromString(xmlRoot.Attributes["AmmoCharge"].Value, typeof(int));

                if (xmlRoot.Attributes["ProjCharge"] != null)
                    _projCharge = (int)ParseHelper.FromString(xmlRoot.Attributes["ProjCharge"].Value, typeof(int));
            }
        }

        public virtual void ResolveReferences()
        {
            if (_ammo != null && adders.NullOrEmpty())
            {
                adders = new List<ThingDefCountClass>();
                adders.Add(new ThingDefCountClass(_ammo, _ammoCharge));
            }

            if (_projectile != null && users.NullOrEmpty())
            {
                var projList = new List<ThingDefCountClass>();
                projList.Add(new ThingDefCount(_projectile, 1));

                users = new List<ChargeUser>();
                users.Add(new ChargeUser() { chargesUsed = _projCharge, projectiles = projList });
            }
        }

        public virtual IEnumerable<string> ConfigErrors()
        {
            if (adders.Count > 1 || users.Count > 1)
                yield return "current AmmoLink class does not support multiple adders and/or users, please use AmmoLink_MultiAmmo";
        }
        #endregion

        /* From:
        <ammoTypes>
            <Shell_HighExplosive>Bullet_81mmMortarShell_HE</Shell_HighExplosive>
        </ammoTypes>
        */

        /* To:
        <ammoTypes>
            <li>
                <adders>
                    <li>
                        <Shell_HighExplosive>1</Shell_HighExplosive>
                        <isFallback>True</isFallback>
                    </li>
                </adders>
                <users>
                    <li>
                        <chargesUsed>1</chargesUsed>
                        <projectiles>
                            <li>
                                <Bullet_81mmMortarShell_HE>1</Bullet_81mmMortarShell_HE>
                            </li>
                        </projectiles>
                    </li>
                </users>
            </li>
        </ammoTypes>
        */

        /*
        public override string ToString()
        {
            return "("
                + (ammo == null ? "null" : ammo.defName)
              //+ (amount > 1 ? "x" + amount + " -> " : " -> ")
                + (projectile == null ? "null" : projectile.defName + ")");
        }

        public override int GetHashCode()
        {
            return ammo.shortHash + projectile.shortHash + amount << 16;
        }*/
    }
}
