// 
//  Copyright (c) Microsoft Corporation. All rights reserved. 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  

namespace Microsoft.PowerShell.OneGet.CmdLets {
    using System.Collections.Generic;
    using System.Diagnostics.Eventing;
    using System.Linq;
    using System.Management.Automation;
    using Microsoft.OneGet.Packaging;
    using Microsoft.OneGet.Providers.Package;
    using Microsoft.OneGet.Utility.Extensions;

    [Cmdlet(VerbsCommon.Get, Constants.PackageSourceNoun)]
    public sealed class GetPackageSource : CmdletWithProvider {
        private readonly List<string> _warnings = new List<string>();

        public GetPackageSource()
            : base(new[] {
                OptionCategory.Provider, OptionCategory.Source
            }) {
        }

        [Parameter(Position = 0)]
        public string Name {get; set;}

        [Parameter]
        public string Location {get; set;}

        private IEnumerable<string> _sources {
            get {
                if (!string.IsNullOrEmpty(Name)) {
                    yield return Name;
                }

                if (!string.IsNullOrEmpty(Location)) {
                    yield return Location;
                }
            }
        }
        public override IEnumerable<string> Sources {
            get {
                return _sources.ByRef();
            }
        }

        private bool _found = false;

        private bool WriteSources(IEnumerable<PackageSource> sources) {
            foreach (var source in sources) {
                _found = true;
                WriteObject(source);
            }
            return _found;
        }

        private List<PackageSource> _unregistered = new List<PackageSource>();
        private HashSet<string>  _providersUsed = new HashSet<string>();
        private bool _noName;
        private bool _noLocation;
        private bool _noCriteria;

        public override bool ProcessRecordAsync() {
            _noName = _noName || string.IsNullOrEmpty(Name);
            _noLocation = _noLocation ||  string.IsNullOrEmpty(Location);
            _noCriteria = _noName && _noLocation;

            foreach (var provider in SelectedProviders) {
                if (Stopping) {
                    return false;
                }

                using (var sources = CancelWhenStopped(provider.ResolvePackageSources(this))) {

                    if (_noCriteria) {
                        // no criteria means just return whatever we found
                        if (WriteSources(sources)) {
                            return true;
                        }
                    } else {
                        var all = sources.ToArray();
                        var registered = all.Where(each => each.IsRegistered);
                        

                        if (_noName) {
                            // just location was specified
                            if (WriteSources(registered.Where(each => each.Location.EqualsIgnoreCase(Location)))) {
                                return true;
                            }
                        } else {
                            // source was specified (check both name and location fields for match)
                            if (WriteSources(registered.Where(each => each.Name.EqualsIgnoreCase(Name) || each.Location.EqualsIgnoreCase(Name)))) {
                                return true;
                            }
                        }
                        // we haven't returned anything to the user yet...
                        // hold on to the unregistered ones. Might need these at the end.
                        _unregistered.AddRangeLocked(all.Where(each => !each.IsRegistered));
                    }



                    if (!string.IsNullOrEmpty(Name)) {
                        if (!string.IsNullOrEmpty(Location)) {
                            _warnings.Add(FormatMessageString(Constants.ProviderReturnedNoPackageSourcesNameLocation, provider.ProviderName, Name, Location));
                            continue;
                        }
                        _warnings.Add(FormatMessageString(Constants.ProviderReturnedNoPackageSourcesName, provider.ProviderName, Name));
                        continue;
                    }

                    if (!string.IsNullOrEmpty(Location)) {
                        _warnings.Add(FormatMessageString(Constants.ProviderReturnedNoPackageSourcesLocation, provider.ProviderName, Location));
                        continue;
                    }
                    _warnings.Add(FormatMessageString(Constants.ProviderReturnedNoPackageSources, provider.ProviderName));
                }
            }

            return true;
        }

        public override bool EndProcessingAsync() {
            if (!_found) {
                if (_noCriteria) {
                    // no criteria means just return whatever we found
                    if (WriteSources(_unregistered)) {
                        return true;
                    }
                    Warning(Constants.NoSourcesFoundNoCriteria);
                    return true;
                }

                if (_noName) {
                    // just location was specified
                    if (WriteSources(_unregistered.Where(each => each.Location.EqualsIgnoreCase(Location)))) {
                        return true;
                    }
                    Warning(Constants.NoSourcesFoundMatchingLocation,Location);
                    return true;
                }

                // source was specified (check both name and location fields for match)
                if (WriteSources(_unregistered.Where(each => each.Name.EqualsIgnoreCase(Name) || each.Location.EqualsIgnoreCase(Name)))) {
                    return true;
                }
                Warning(Constants.NoSourcesFoundMatchingName,Name);
                return true;

            }
            return true;
        }
    }
}