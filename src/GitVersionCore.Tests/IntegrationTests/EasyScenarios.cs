namespace GitVersionCore.Tests.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using GitTools.Testing;
    using GitVersion;
    using GitVersion.Configuration;
    using LibGit2Sharp;
    using NUnit.Framework;
    using GitVersion.Extensions;

    [TestFixture]
    public class EasyScenarios : TestBase
    {
        static Config config = new Config
        {
            Branches =
            {
                { "master", new BranchConfig
                    {
                        Tag = "beta",
                        Regex = "^master$",
                        TracksReleaseBranches = true, // turning this on will make the master version from when the last release branch was created,
                        Increment = IncrementStrategy.Minor
                    }
                },
                { "release", new BranchConfig
                    {
                        Tag = "",
                        Regex = "^releases?[/-]",
                    }
                },
                { "feature", new BranchConfig
                    {
                        Tag = "useBranchName",
                        Regex = "^features?[/-]"
                    }
                },
                { "bugfix", new BranchConfig
                    {
                        Tag = "useBranchName",
                        Regex = "^bugfix(es)?[/-]",
                        SourceBranches = new List<string> { "master" },
                    }
                },
                { "hotfix", new BranchConfig
                    {
                        Tag = "useBranchName",
                        Regex = "^hotfix(es)?[/-]",
                        TracksReleaseBranches = true,
                    }
                },
                //{ "pull-request", new BranchConfig
                //    {
                //        Tag = "PullRequest",
                //        Regex = "^(pull|pull\\-requests|pr)[/-]"
                //    }
                //}
            }
        };


        [Test]
        public void Master_has_a_minor_version_number_which_is_plus_one_since_last_release_branch_when_not_merged()
        {
            using var fixture = new EmptyRepositoryFixture();
            fixture.Repository.MakeACommit();
            fixture.Repository.CreateBranch("release/1.26");
            fixture.Checkout("master");
            fixture.Repository.MakeACommit();

            fixture.AssertFullSemver(config, "1.27.0-beta.1+1");
        }

        [Test]
        public void Merge_release_branch_back_to_master()
        {
            using var fixture = new EmptyRepositoryFixture();
            fixture.Repository.MakeACommit();

            fixture.Repository.CreateBranch("release/1.26");
            fixture.Checkout("release/1.26");
            fixture.Repository.MakeACommit();

            fixture.Checkout("master");
            fixture.Repository.MakeACommit();

            fixture.Repository.MergeNoFF("release/1.26");

            fixture.AssertFullSemver(config, "1.27.0-beta.1+3");
        }

        [Test]
        public void Bugfix_on_release_branch()
        {
            using var fixture = new EmptyRepositoryFixture();
            fixture.Repository.MakeACommit();

            fixture.Repository.CreateBranch("release/1.26");
            fixture.Checkout("release/1.26");
            fixture.Repository.MakeACommit();
            fixture.AssertFullSemver(config, "1.26.0+1");
            //fixture.ApplyTag("1.26");

            fixture.Repository.CreateBranch("hotfix/on-release-branch");
            fixture.Checkout("hotfix/on-release-branch");
            fixture.Repository.MakeACommit();
            fixture.AssertFullSemver(config, "1.26.1-on-release-branch.1+1");

            fixture.Repository.MakeACommit();
            fixture.AssertFullSemver(config, "1.26.1-on-release-branch.1+2");

            var commit = fixture.Repository.CreatePullRequestRef("hotfix/on-release-branch", "release/1.26", normalise: true);

            fixture.AssertFullSemver(config, "1.27.0-PullRequest0002.3"); //where did 27 come from
            fixture.Checkout("release/1.26");
            fixture.Repository.Merge(commit, new Signature("test", "test@test.no", DateTimeOffset.UtcNow));
            fixture.AssertFullSemver(config, "1.26.0+4");
        }

        [Test]
        public void Bugfix_on_master_branch()
        {
            using var fixture = new EmptyRepositoryFixture();
            fixture.Repository.MakeACommit();

            fixture.Repository.CreateBranch("release/1.26");
            fixture.Checkout("release/1.26");
            fixture.AssertFullSemver(config, "1.26.0+0");
            //fixture.ApplyTag("1.26");

            fixture.Checkout("master");
            //fixture.AssertFullSemver(config, "1.26.0");
            fixture.Repository.MakeACommit();
            fixture.AssertFullSemver(config, "1.27.0-beta.1+1");

            fixture.Repository.CreateBranch("bugfix/on-master");
            fixture.Checkout("bugfix/on-master");
            fixture.Repository.MakeACommit();
            fixture.AssertFullSemver(config, "1.27.0-on-master.1+2");

            var commit = fixture.Repository.CreatePullRequestRef("bugfix/on-master", "master", normalise: true);

            fixture.AssertFullSemver(config, "1.27.0-PullRequest0002.3");
            fixture.Checkout("master");
            fixture.Repository.Merge(commit, new Signature("test", "test@test.no", DateTimeOffset.UtcNow));
            fixture.AssertFullSemver(config, "1.27.0-beta.1+3");
        }

        [Test]
        public void Pull_request_of_feature_branch_to_master_both_feature_and_master_has_commits()
        {
            using var fixture = new EmptyRepositoryFixture();
            fixture.Repository.MakeACommit();
            fixture.Repository.CreateBranch("release/1.26");
            fixture.Checkout("master");

            fixture.Repository.CreateBranch("feature/some-new-feature");

            fixture.Checkout("master");
            fixture.Repository.MakeCommits(4);

            fixture.Checkout("feature/some-new-feature");
            fixture.Repository.MakeCommits(3);

            var commit = fixture.Repository.CreatePullRequestRef("feature/some-new-feature", "master", normalise: true);

            fixture.AssertFullSemver(config, "1.27.0-PullRequest0002.8");
            fixture.Checkout("master");
            fixture.Repository.Merge(commit, new Signature("test", "test@test.no", DateTimeOffset.UtcNow));
            fixture.AssertFullSemver(config, "1.27.0-beta.1+8");
        }

        [Test]
        public void Feature_branch_from_master_no_commits()
        {
            using var fixture = new EmptyRepositoryFixture();
            fixture.Repository.MakeACommit();
            fixture.Repository.CreateBranch("release/1.26");
            fixture.Checkout("master");
            fixture.Repository.MakeACommit();

            fixture.Repository.CreateBranch("feature/some-new-feature");
            fixture.Checkout("feature/some-new-feature");

            fixture.AssertFullSemver(config, "1.27.0-some-new-feature.1+1");
        }

        [Test]
        public void Feature_branch_from_master_with_commits()
        {
            using var fixture = new EmptyRepositoryFixture();
            fixture.Repository.MakeACommit();
            fixture.Repository.CreateBranch("release/1.26");
            fixture.Checkout("master");
            fixture.Repository.MakeACommit();

            fixture.Repository.CreateBranch("feature/some-new-feature");
            fixture.Checkout("feature/some-new-feature");
            fixture.Repository.MakeACommit();

            fixture.AssertFullSemver(config, "1.27.0-some-new-feature.1+2");
        }


        [Test]
        public void Release_branch_from_master_no_commits()
        {
            using var fixture = new EmptyRepositoryFixture();
            fixture.Repository.MakeACommit();
            fixture.Repository.CreateBranch("release/1.26");
            fixture.Checkout("release/1.26");

            fixture.AssertFullSemver(config, "1.26.0+0");
        }

        [Test]
        public void Release_branch_from_master_with_commits()
        {
            using var fixture = new EmptyRepositoryFixture();
            fixture.Repository.MakeACommit();
            fixture.Repository.CreateBranch("release/1.26");
            fixture.Checkout("release/1.26");
            fixture.Repository.MakeACommit();

            fixture.AssertFullSemver(config, "1.26.0+1");
        }

    }
}
