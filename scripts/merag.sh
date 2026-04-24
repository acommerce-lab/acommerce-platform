git checkout main
git pull origin main

for branch in $(git branch -r | grep -v '\->'); do
    branch_name=$(echo $branch | sed 's/origin\///')

    if [ "$branch_name" != "main" ]; then
        echo "Merging $branch_name..."
        git merge origin/$branch_name -X theirs --no-edit
    fi
done

git push origin main